using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using VaporTrail.Options;
using VaporTrail.Services;

namespace VaporTrail.Tests;

public class SqliteSteamCatalogServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"vaportrail-cat-{Guid.NewGuid():N}");

    [Fact]
    public async Task SyncFromStreamAsync_GzippedJson_PersistsAndQueriesCatalog()
    {
        const string json = """
        [
          {
            "steam_appid": 999001,
            "name": "Test Odyssey",
            "genres": ["Action", "RPG", "Action"],
            "tags": ["Open World", "Story Rich"],
            "developers": ["Studio Alpha"],
            "publishers": ["Pub Alpha"],
            "release_date": "2024-05-10",
            "price_usd": 2999,
            "price_final_usd": 1499,
            "steam_reviews_percent": 94,
            "metacritic_score": 88,
            "hltb_main_hours": 24.5,
            "average_playtime_hours": 31.2
          },
          {
            "steam_appid": null,
            "name": "GOG Exclusive Without Steam Id",
            "genres": ["Adventure"]
          }
        ]
        """;

        using var service = CreateService();
        Assert.False(service.GetStatus().IsReady);

        await using var gzStream = ToGzipStream(json);
        var imported = await service.SyncFromStreamAsync(gzStream, isGzip: true);

        Assert.Equal(1, imported);

        var status = service.GetStatus();
        Assert.True(status.IsReady);
        Assert.False(status.IsSyncing);
        Assert.Equal(1, status.GameCount);
        Assert.NotNull(status.SyncedAtUtc);
        Assert.Null(status.LastError);

        // Query both a synced SQLite game and an offline DemoSteamCatalogSeed game (220 = Half-Life 2).
        var map = await service.GetGamesByAppIdsAsync([999001, 220, 777777]);

        Assert.Equal(2, map.Count);

        var synced = map[999001];
        Assert.Equal("Test Odyssey", synced.Name);
        Assert.Equal(["Action", "RPG"], synced.Genres);
        Assert.Equal(["Open World", "Story Rich"], synced.Tags);
        Assert.Equal(["Studio Alpha"], synced.Developers);
        Assert.Equal(2999, synced.PriceUsdCents);
        Assert.Equal(1499, synced.PriceFinalUsdCents);
        Assert.Equal(94, synced.SteamReviewsPercent);
        Assert.Equal(88, synced.MetacriticScore);
        Assert.Equal(24.5, synced.HltbMainHours);

        Assert.Equal("Half-Life 2", map[220].Name);
        Assert.False(map.ContainsKey(777777));
    }

    private SqliteSteamCatalogService CreateService()
    {
        Directory.CreateDirectory(_tempDir);
        var options = Microsoft.Extensions.Options.Options.Create(new CatalogOptions
        {
            DatabasePath = Path.Combine(_tempDir, "catalog.sqlite"),
            AutoSyncOnStartup = false
        });

        return new SqliteSteamCatalogService(
            new HttpClient(),
            options,
            new StubHostEnvironment(_tempDir),
            NullLogger<SqliteSteamCatalogService>.Instance);
    }

    private static MemoryStream ToGzipStream(string text)
    {
        var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            gz.Write(bytes, 0, bytes.Length);
        }
        output.Position = 0;
        return output;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private sealed class StubHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "VaporTrail.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
