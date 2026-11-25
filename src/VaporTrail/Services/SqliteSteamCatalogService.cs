using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using VaporTrail.Models;
using VaporTrail.Options;

namespace VaporTrail.Services;

public sealed class SqliteSteamCatalogService : ISteamCatalogService, IDisposable
{
    private const int SqliteBatchSize = 400;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly CatalogOptions _options;
    private readonly ILogger<SqliteSteamCatalogService> _logger;
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private volatile bool _isSyncing;
    private int _gameCount;
    private DateTimeOffset? _syncedAtUtc;
    private string? _lastError;

    public SqliteSteamCatalogService(
        HttpClient httpClient,
        IOptions<CatalogOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<SqliteSteamCatalogService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _dbPath = Path.IsPathRooted(_options.DatabasePath)
            ? _options.DatabasePath
            : Path.Combine(hostEnvironment.ContentRootPath, _options.DatabasePath);

        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        EnsureSchemaAndLoadStatus();
    }

    public SteamCatalogStatus GetStatus() =>
        new(
            IsReady: _gameCount > 0,
            IsSyncing: _isSyncing,
            GameCount: _gameCount,
            SyncedAtUtc: _syncedAtUtc,
            LastError: _lastError
        );

    public async Task<IReadOnlyDictionary<int, SteamCatalogGame>> GetGamesByAppIdsAsync(
        IEnumerable<int> appIds,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = appIds.Where(id => id > 0).Distinct().ToList();
        var results = new Dictionary<int, SteamCatalogGame>(distinctIds.Count);

        if (distinctIds.Count == 0)
        {
            return results;
        }

        if (_gameCount > 0 && File.Exists(_dbPath))
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            for (var offset = 0; offset < distinctIds.Count; offset += SqliteBatchSize)
            {
                var batch = distinctIds.Skip(offset).Take(SqliteBatchSize).ToList();
                var placeholders = string.Join(", ", Enumerable.Range(0, batch.Count).Select(i => $"$p{i}"));

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"""
                    SELECT app_id, name, genres_json, tags_json, developers_json, publishers_json,
                           release_date, price_usd, price_final_usd, steam_reviews_percent,
                           metacritic_score, hltb_main_hours, average_playtime_hours
                    FROM games
                    WHERE app_id IN ({placeholders});
                    """;

                for (var i = 0; i < batch.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"$p{i}", batch[i]);
                }

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var appId = reader.GetInt32(0);
                    results[appId] = new SteamCatalogGame(
                        AppId: appId,
                        Name: reader.GetString(1),
                        Genres: DeserializeStringList(reader.IsDBNull(2) ? null : reader.GetString(2)),
                        Tags: DeserializeStringList(reader.IsDBNull(3) ? null : reader.GetString(3)),
                        Developers: DeserializeStringList(reader.IsDBNull(4) ? null : reader.GetString(4)),
                        Publishers: DeserializeStringList(reader.IsDBNull(5) ? null : reader.GetString(5)),
                        ReleaseDate: reader.IsDBNull(6) ? null : reader.GetString(6),
                        PriceUsdCents: reader.IsDBNull(7) ? null : reader.GetInt32(7),
                        PriceFinalUsdCents: reader.IsDBNull(8) ? null : reader.GetInt32(8),
                        SteamReviewsPercent: reader.IsDBNull(9) ? null : reader.GetInt32(9),
                        MetacriticScore: reader.IsDBNull(10) ? null : reader.GetInt32(10),
                        HltbMainHours: reader.IsDBNull(11) ? null : reader.GetDouble(11),
                        AveragePlaytimeHours: reader.IsDBNull(12) ? null : reader.GetDouble(12)
                    );
                }
            }
        }

        // Fill any missing demo/known titles from the offline seed so Demo Mode and cold starts still render rich analytics.
        foreach (var id in distinctIds)
        {
            if (!results.ContainsKey(id) && DemoSteamCatalogSeed.TryGet(id, out var seeded))
            {
                results[id] = seeded;
            }
        }

        return results;
    }

    public async Task<SteamCatalogStatus> SyncFromRemoteAsync(
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (!force && IsFresh())
        {
            return GetStatus();
        }

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            if (!force && IsFresh())
            {
                return GetStatus();
            }

            _isSyncing = true;
            _lastError = null;

            _logger.LogInformation("Syncing Steam game catalog from {DumpUrl}...", _options.DumpUrl);

            using var response = await _httpClient.GetAsync(
                _options.DumpUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var isGzip = _options.DumpUrl.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
            var count = await SyncFromStreamCoreAsync(networkStream, isGzip, cancellationToken);

            _logger.LogInformation("Steam game catalog sync completed with {Count:N0} games.", count);
            return GetStatus();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _lastError = ex.Message;
            _logger.LogWarning(ex, "Failed to sync Steam game catalog from {DumpUrl}", _options.DumpUrl);
            return GetStatus();
        }
        finally
        {
            _isSyncing = false;
            _syncLock.Release();
        }
    }

    public async Task<int> SyncFromStreamAsync(
        Stream stream,
        bool isGzip,
        CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            _isSyncing = true;
            _lastError = null;
            return await SyncFromStreamCoreAsync(stream, isGzip, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _lastError = ex.Message;
            throw;
        }
        finally
        {
            _isSyncing = false;
            _syncLock.Release();
        }
    }

    private async Task<int> SyncFromStreamCoreAsync(
        Stream inputStream,
        bool isGzip,
        CancellationToken cancellationToken)
    {
        await using Stream jsonStream = isGzip
            ? new GZipStream(inputStream, CompressionMode.Decompress, leaveOpen: true)
            : inputStream;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using (var pragmaCmd = conn.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA synchronous = NORMAL; PRAGMA cache_size = -8000;";
            await pragmaCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        const int commitBatchSize = 5000;
        var imported = 0;
        var totalRows = 0;
        var syncedAt = DateTimeOffset.UtcNow;

        var tx = (SqliteTransaction)await conn.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var upsertCmd = conn.CreateCommand();
            upsertCmd.Transaction = tx;
            upsertCmd.CommandText = """
                INSERT INTO games (
                    app_id, name, genres_json, tags_json, developers_json, publishers_json,
                    release_date, price_usd, price_final_usd, steam_reviews_percent,
                    metacritic_score, hltb_main_hours, average_playtime_hours
                ) VALUES (
                    $appId, $name, $genres, $tags, $developers, $publishers,
                    $releaseDate, $priceUsd, $priceFinalUsd, $reviewsPercent,
                    $metacritic, $hltbMain, $avgPlaytime
                )
                ON CONFLICT(app_id) DO UPDATE SET
                    name = excluded.name,
                    genres_json = excluded.genres_json,
                    tags_json = excluded.tags_json,
                    developers_json = excluded.developers_json,
                    publishers_json = excluded.publishers_json,
                    release_date = excluded.release_date,
                    price_usd = excluded.price_usd,
                    price_final_usd = excluded.price_final_usd,
                    steam_reviews_percent = excluded.steam_reviews_percent,
                    metacritic_score = excluded.metacritic_score,
                    hltb_main_hours = excluded.hltb_main_hours,
                    average_playtime_hours = excluded.average_playtime_hours;
                """;

            var pAppId = upsertCmd.Parameters.Add("$appId", SqliteType.Integer);
            var pName = upsertCmd.Parameters.Add("$name", SqliteType.Text);
            var pGenres = upsertCmd.Parameters.Add("$genres", SqliteType.Text);
            var pTags = upsertCmd.Parameters.Add("$tags", SqliteType.Text);
            var pDevelopers = upsertCmd.Parameters.Add("$developers", SqliteType.Text);
            var pPublishers = upsertCmd.Parameters.Add("$publishers", SqliteType.Text);
            var pReleaseDate = upsertCmd.Parameters.Add("$releaseDate", SqliteType.Text);
            var pPriceUsd = upsertCmd.Parameters.Add("$priceUsd", SqliteType.Integer);
            var pPriceFinalUsd = upsertCmd.Parameters.Add("$priceFinalUsd", SqliteType.Integer);
            var pReviewsPercent = upsertCmd.Parameters.Add("$reviewsPercent", SqliteType.Integer);
            var pMetacritic = upsertCmd.Parameters.Add("$metacritic", SqliteType.Integer);
            var pHltbMain = upsertCmd.Parameters.Add("$hltbMain", SqliteType.Real);
            var pAvgPlaytime = upsertCmd.Parameters.Add("$avgPlaytime", SqliteType.Real);

            await upsertCmd.PrepareAsync(cancellationToken);

            await foreach (var entry in JsonSerializer.DeserializeAsyncEnumerable<SteamDbRawEntry>(
                jsonStream, JsonOptions, cancellationToken))
            {
                if (entry?.SteamAppId is not > 0)
                {
                    continue;
                }

                pAppId.Value = entry.SteamAppId.Value;
                pName.Value = entry.Name ?? $"App {entry.SteamAppId.Value}";
                pGenres.Value = SerializeNormalizedList(entry.Genres);
                pTags.Value = SerializeNormalizedList(entry.Tags);
                pDevelopers.Value = SerializeNormalizedList(entry.Developers);
                pPublishers.Value = SerializeNormalizedList(entry.Publishers);
                pReleaseDate.Value = (object?)entry.ReleaseDate ?? DBNull.Value;
                pPriceUsd.Value = (object?)entry.PriceUsd ?? DBNull.Value;
                pPriceFinalUsd.Value = (object?)entry.PriceFinalUsd ?? DBNull.Value;
                pReviewsPercent.Value = (object?)entry.SteamReviewsPercent ?? DBNull.Value;
                pMetacritic.Value = (object?)entry.MetacriticScore ?? DBNull.Value;
                pHltbMain.Value = (object?)entry.HltbMainHours ?? DBNull.Value;
                pAvgPlaytime.Value = (object?)entry.AveragePlaytimeHours ?? DBNull.Value;

                await upsertCmd.ExecuteNonQueryAsync(cancellationToken);
                imported++;

                if (imported % commitBatchSize == 0)
                {
                    await tx.CommitAsync(cancellationToken);
                    await tx.DisposeAsync();
                    tx = (SqliteTransaction)await conn.BeginTransactionAsync(cancellationToken);
                    upsertCmd.Transaction = tx;
                }
            }

            syncedAt = DateTimeOffset.UtcNow;

            await using var countCmd = conn.CreateCommand();
            countCmd.Transaction = tx;
            countCmd.CommandText = "SELECT COUNT(*) FROM games;";
            totalRows = Convert.ToInt32(await countCmd.ExecuteScalarAsync(cancellationToken));

            await using var metaCmd = conn.CreateCommand();
            metaCmd.Transaction = tx;
            metaCmd.CommandText = """
                INSERT INTO catalog_meta (key, value) VALUES ('synced_at_utc', $syncedAt)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                INSERT INTO catalog_meta (key, value) VALUES ('row_count', $rowCount)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
            metaCmd.Parameters.AddWithValue("$syncedAt", syncedAt.ToString("O"));
            metaCmd.Parameters.AddWithValue("$rowCount", totalRows.ToString());
            await metaCmd.ExecuteNonQueryAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }
        finally
        {
            await tx.DisposeAsync();
        }

        await using (var checkpointCmd = conn.CreateCommand())
        {
            checkpointCmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await checkpointCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        _gameCount = totalRows;
        _syncedAtUtc = syncedAt;
        _lastError = null;

        return imported;
    }

    private bool IsFresh()
    {
        if (_gameCount <= 0 || _syncedAtUtc is null)
        {
            return false;
        }

        var maxAge = TimeSpan.FromHours(Math.Max(1, _options.RefreshIntervalHours));
        return DateTimeOffset.UtcNow - _syncedAtUtc.Value < maxAge;
    }

    private void EnsureSchemaAndLoadStatus()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS games (
                app_id INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                genres_json TEXT NOT NULL,
                tags_json TEXT NOT NULL,
                developers_json TEXT NOT NULL,
                publishers_json TEXT NOT NULL,
                release_date TEXT NULL,
                price_usd INTEGER NULL,
                price_final_usd INTEGER NULL,
                steam_reviews_percent INTEGER NULL,
                metacritic_score INTEGER NULL,
                hltb_main_hours REAL NULL,
                average_playtime_hours REAL NULL
            );
            CREATE TABLE IF NOT EXISTS catalog_meta (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM games;";
        _gameCount = Convert.ToInt32(countCmd.ExecuteScalar());

        using var metaCmd = conn.CreateCommand();
        metaCmd.CommandText = "SELECT value FROM catalog_meta WHERE key = 'synced_at_utc';";
        var rawDate = metaCmd.ExecuteScalar() as string;
        if (!string.IsNullOrWhiteSpace(rawDate) && DateTimeOffset.TryParse(rawDate, out var parsed))
        {
            _syncedAtUtc = parsed;
        }
    }

    private static string SerializeNormalizedList(List<string>? items)
    {
        if (items is null || items.Count == 0)
        {
            return "[]";
        }

        var cleaned = items
            .Select(s => s?.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return JsonSerializer.Serialize(cleaned);
    }

    private static IReadOnlyList<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Dispose()
    {
        _syncLock.Dispose();
    }
}
