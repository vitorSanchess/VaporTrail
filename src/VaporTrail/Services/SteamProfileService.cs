using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VaporTrail.Models;
using VaporTrail.Options;

namespace VaporTrail.Services;

public sealed class SteamProfileService : ISteamProfileService
{
    private const int MaxGamesToReturn = 6;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ISteamCatalogService _catalogService;
    private readonly SteamOptions _options;
    private readonly ILogger<SteamProfileService> _logger;

    public SteamProfileService(
        HttpClient httpClient,
        IMemoryCache cache,
        ISteamCatalogService catalogService,
        IOptions<SteamOptions> options,
        ILogger<SteamProfileService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _catalogService = catalogService;
        _options = options.Value;
        _logger = logger;
    }

    public SteamServiceStatus GetStatus()
    {
        var isDemo = !_options.HasApiKey;
        return new SteamServiceStatus(
            Mode: isDemo ? "demo" : "live",
            IsDemoMode: isDemo,
            CacheDurationMinutes: Math.Max(1, _options.CacheDurationMinutes),
            SampleProfiles: DemoSteamDataProvider.GetSampleHints(isDemo),
            Catalog: _catalogService.GetStatus()
        );
    }

    public async Task<SteamLookupResult> GetProfileAsync(
        string? rawQuery,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        var parsed = SteamInputParser.Parse(rawQuery);
        if (!parsed.IsSuccess)
        {
            return SteamLookupResult.Fail(400, parsed.ErrorMessage!);
        }

        var queryCacheKey = BuildQueryCacheKey(parsed);
        if (!bypassCache && _cache.TryGetValue<SteamProfileResponse>(queryCacheKey, out var cached) && cached is not null)
        {
            return SteamLookupResult.Ok(cached with { FromCache = true });
        }

        var now = DateTimeOffset.UtcNow;

        if (!_options.HasApiKey)
        {
            var demoResult = DemoSteamDataProvider.ResolveDemoProfile(parsed, now);
            if (demoResult.IsSuccess && demoResult.Profile is not null)
            {
                var enriched = demoResult.Profile;
                if (!enriched.GamesArePrivate)
                {
                    var library = DemoSteamDataProvider.GetDemoLibraryForBreakdown(enriched);
                    var catalogGames = await _catalogService.GetGamesByAppIdsAsync(
                        library.Select(x => x.AppId),
                        cancellationToken);
                    enriched = enriched with
                    {
                        Breakdown = LibraryBreakdownBuilder.Build(library, catalogGames)
                    };
                    demoResult = SteamLookupResult.Ok(enriched);
                }

                CacheProfile(queryCacheKey, enriched);
            }
            return demoResult;
        }

        try
        {
            var steamId = parsed.Value;
            if (parsed.Kind == SteamInputKind.VanityName)
            {
                var vanityResult = await ResolveVanityUrlAsync(parsed.Value, cancellationToken);
                if (!vanityResult.IsSuccess)
                {
                    return vanityResult;
                }
                steamId = vanityResult.Profile!.SteamId;
            }

            var steamIdCacheKey = BuildSteamIdCacheKey(steamId);
            if (!bypassCache && _cache.TryGetValue<SteamProfileResponse>(steamIdCacheKey, out var cachedById) && cachedById is not null)
            {
                CacheAlias(queryCacheKey, cachedById);
                return SteamLookupResult.Ok(cachedById with { FromCache = true });
            }

            // Fetch all 7 Steam Web API endpoints in parallel.
            var summaryTask = FetchPlayerSummaryAsync(steamId, cancellationToken);
            var levelTask = FetchSteamLevelAsync(steamId, cancellationToken);
            var recentTask = FetchRecentlyPlayedAsync(steamId, cancellationToken);
            var ownedTask = FetchOwnedGamesAsync(steamId, cancellationToken);
            var badgesTask = FetchBadgesAsync(steamId, cancellationToken);
            var bansTask = FetchPlayerBansAsync(steamId, cancellationToken);
            var friendCountTask = FetchFriendCountAsync(steamId, cancellationToken);

            await Task.WhenAll(summaryTask, levelTask, recentTask, ownedTask, badgesTask, bansTask, friendCountTask);

            var player = await summaryTask;
            if (player is null)
            {
                return SteamLookupResult.Fail(
                    404,
                    $"No Steam profile was found for SteamID '{steamId}'.");
            }

            var steamLevel = await levelTask;
            var recentEnvelope = await recentTask;
            var ownedEnvelope = await ownedTask;
            var badgesBody = await badgesTask;
            var banDto = await bansTask;
            var friendCount = await friendCountTask;

            var isPublic = player.CommunityVisibilityState == 3;
            var (statusText, statusKind) = MapPersonaStatus(player);

            var ownedBody = ownedEnvelope?.Response;
            var hasOwnedData = ownedBody?.GameCount is not null || ownedBody?.Games is not null;
            var gamesArePrivate = !isPublic || !hasOwnedData;

            var ownedGames = ownedBody?.Games ?? [];
            int? totalOwnedGames = gamesArePrivate
                ? null
                : ownedBody?.GameCount ?? ownedGames.Count;

            double? totalPlaytimeHours = gamesArePrivate
                ? null
                : Math.Round(ownedGames.Sum(g => (long)g.PlaytimeForever) / 60.0, 1);

            var topPlayed = gamesArePrivate
                ? []
                : ownedGames
                    .OrderByDescending(g => g.PlaytimeForever)
                    .Take(MaxGamesToReturn)
                    .Select(MapGameSummary)
                    .ToList();

            var recentlyPlayed = (recentEnvelope?.Response?.Games ?? [])
                .Take(MaxGamesToReturn)
                .Select(MapGameSummary)
                .ToList();

            var standing = BuildAccountStanding(banDto);
            var progression = BuildAccountProgression(badgesBody, friendCount);

            LibraryBreakdown? breakdown = null;
            if (!gamesArePrivate && ownedGames.Count > 0)
            {
                var ownedPairs = ownedGames
                    .Select(g => (g.AppId, Math.Round(g.PlaytimeForever / 60.0, 1)))
                    .ToList();
                var catalogGames = await _catalogService.GetGamesByAppIdsAsync(
                    ownedPairs.Select(x => x.AppId),
                    cancellationToken);
                breakdown = LibraryBreakdownBuilder.Build(ownedPairs, catalogGames);
            }

            var profile = new SteamProfileResponse(
                SteamId: player.SteamId,
                PersonaName: player.PersonaName,
                RealName: string.IsNullOrWhiteSpace(player.RealName) ? null : player.RealName,
                AvatarUrl: player.AvatarFull ?? player.AvatarMedium ?? player.Avatar ?? string.Empty,
                ProfileUrl: player.ProfileUrl,
                PersonaStateCode: player.PersonaState,
                StatusText: statusText,
                StatusKind: statusKind,
                CurrentGameName: string.IsNullOrWhiteSpace(player.GameExtraInfo) ? null : player.GameExtraInfo,
                CurrentGameId: string.IsNullOrWhiteSpace(player.GameId) ? null : player.GameId,
                CountryCode: string.IsNullOrWhiteSpace(player.LocCountryCode) ? null : player.LocCountryCode,
                MemberSinceUtc: player.TimeCreated is > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(player.TimeCreated.Value)
                    : null,
                LastLogoffUtc: player.LastLogoff is > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(player.LastLogoff.Value)
                    : null,
                IsPublic: isPublic,
                SteamLevel: steamLevel ?? badgesBody?.PlayerLevel,
                GamesArePrivate: gamesArePrivate,
                TotalOwnedGames: totalOwnedGames,
                TotalPlaytimeHours: totalPlaytimeHours,
                RecentlyPlayed: recentlyPlayed,
                TopPlayedGames: topPlayed,
                IsDemoData: false,
                FromCache: false,
                FetchedAtUtc: now,
                Standing: standing,
                Progression: progression,
                Breakdown: breakdown
            );

            CacheProfile(queryCacheKey, profile);
            return SteamLookupResult.Ok(profile);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Steam Web API request failed for query {Query}", parsed.Value);
            return SteamLookupResult.Fail(
                502,
                "Could not reach the Steam Web API. Verify your API key and network connection, then try again.");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Steam Web API timed out for query {Query}", parsed.Value);
            return SteamLookupResult.Fail(
                502,
                "The Steam Web API timed out. Please try again in a moment.");
        }
    }

    private async Task<SteamLookupResult> ResolveVanityUrlAsync(string vanityName, CancellationToken ct)
    {
        var url = $"ISteamUser/ResolveVanityURL/v1/?key={Uri.EscapeDataString(_options.ApiKey!)}&vanityurl={Uri.EscapeDataString(vanityName)}";
        using var response = await _httpClient.GetAsync(url, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return SteamLookupResult.Fail(502, "Steam Web API rejected the configured API key (HTTP 403).");
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ResolveVanityUrlEnvelope>(JsonOptions, ct);

        if (payload?.Response?.Success == 1 && !string.IsNullOrWhiteSpace(payload.Response.SteamId))
        {
            // Temporarily carry resolved SteamID back in a minimal response.
            return SteamLookupResult.Ok(new SteamProfileResponse(
                payload.Response.SteamId, "", null, "", "", 0, "", "", null, null, null, null, null,
                false, null, true, null, null, [], [], false, false, DateTimeOffset.UtcNow));
        }

        if (payload?.Response?.Success == 42)
        {
            return SteamLookupResult.Fail(404, $"No Steam account found matching vanity URL '{vanityName}'.");
        }

        return SteamLookupResult.Fail(502, payload?.Response?.Message ?? "Failed to resolve Steam vanity URL.");
    }

    private async Task<SteamPlayerDto?> FetchPlayerSummaryAsync(string steamId, CancellationToken ct)
    {
        var url = $"ISteamUser/GetPlayerSummaries/v2/?key={Uri.EscapeDataString(_options.ApiKey!)}&steamids={Uri.EscapeDataString(steamId)}";
        using var response = await _httpClient.GetAsync(url, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new HttpRequestException("Steam Web API rejected the configured API key.", null, response.StatusCode);
        }

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<PlayerSummariesEnvelope>(JsonOptions, ct);
        return envelope?.Response?.Players?.FirstOrDefault();
    }

    private async Task<int?> FetchSteamLevelAsync(string steamId, CancellationToken ct)
    {
        try
        {
            var url = $"IPlayerService/GetSteamLevel/v1/?key={Uri.EscapeDataString(_options.ApiKey!)}&steamid={Uri.EscapeDataString(steamId)}";
            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var envelope = await response.Content.ReadFromJsonAsync<SteamLevelEnvelope>(JsonOptions, ct);
            return envelope?.Response?.PlayerLevel;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Could not fetch Steam level for {SteamId}", steamId);
            return null;
        }
    }

    private async Task<RecentlyPlayedGamesEnvelope?> FetchRecentlyPlayedAsync(string steamId, CancellationToken ct)
    {
        try
        {
            var url = $"IPlayerService/GetRecentlyPlayedGames/v1/?key={Uri.EscapeDataString(_options.ApiKey!)}&steamid={Uri.EscapeDataString(steamId)}&count={MaxGamesToReturn}";
            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<RecentlyPlayedGamesEnvelope>(JsonOptions, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Could not fetch recently played games for {SteamId}", steamId);
            return null;
        }
    }

    private async Task<OwnedGamesEnvelope?> FetchOwnedGamesAsync(string steamId, CancellationToken ct)
    {
        try
        {
            var url = $"IPlayerService/GetOwnedGames/v1/?key={Uri.EscapeDataString(_options.ApiKey!)}&steamid={Uri.EscapeDataString(steamId)}&include_appinfo=true&include_played_free_games=true";
            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<OwnedGamesEnvelope>(JsonOptions, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Could not fetch owned games for {SteamId}", steamId);
            return null;
        }
    }

    private async Task<BadgesBody?> FetchBadgesAsync(string steamId, CancellationToken ct)
    {
        try
        {
            var url = $"IPlayerService/GetBadges/v1/?key={Uri.EscapeDataString(_options.ApiKey!)}&steamid={Uri.EscapeDataString(steamId)}";
            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var envelope = await response.Content.ReadFromJsonAsync<BadgesEnvelope>(JsonOptions, ct);
            return envelope?.Response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Could not fetch badges for {SteamId}", steamId);
            return null;
        }
    }

    private async Task<PlayerBanDto?> FetchPlayerBansAsync(string steamId, CancellationToken ct)
    {
        try
        {
            var url = $"ISteamUser/GetPlayerBans/v1/?key={Uri.EscapeDataString(_options.ApiKey!)}&steamids={Uri.EscapeDataString(steamId)}";
            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var envelope = await response.Content.ReadFromJsonAsync<PlayerBansEnvelope>(JsonOptions, ct);
            return envelope?.Players?.FirstOrDefault();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Could not fetch ban standing for {SteamId}", steamId);
            return null;
        }
    }

    private async Task<int?> FetchFriendCountAsync(string steamId, CancellationToken ct)
    {
        try
        {
            var url = $"ISteamUser/GetFriendList/v1/?key={Uri.EscapeDataString(_options.ApiKey!)}&steamid={Uri.EscapeDataString(steamId)}&relationship=friend";
            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var envelope = await response.Content.ReadFromJsonAsync<FriendListEnvelope>(JsonOptions, ct);
            return envelope?.FriendsList?.Friends?.Count;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Could not fetch friend list for {SteamId}", steamId);
            return null;
        }
    }

    private static AccountStanding? BuildAccountStanding(PlayerBanDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        var economyBan = string.IsNullOrWhiteSpace(dto.EconomyBan) ? "none" : dto.EconomyBan;
        var hasEconomyBan = !economyBan.Equals("none", StringComparison.OrdinalIgnoreCase);
        var hasAnyBan = dto.VacBanned
            || dto.CommunityBanned
            || dto.NumberOfVacBans > 0
            || dto.NumberOfGameBans > 0
            || hasEconomyBan;

        return new AccountStanding(
            VacBanned: dto.VacBanned,
            VacBanCount: dto.NumberOfVacBans,
            GameBanCount: dto.NumberOfGameBans,
            DaysSinceLastBan: hasAnyBan && dto.DaysSinceLastBan > 0 ? dto.DaysSinceLastBan : null,
            CommunityBanned: dto.CommunityBanned,
            EconomyBan: economyBan,
            IsGoodStanding: !hasAnyBan
        );
    }

    private static AccountProgression? BuildAccountProgression(BadgesBody? badges, int? friendCount)
    {
        var hasXp = badges?.PlayerXp is not null;
        var badgeCount = badges?.Badges?.Count;

        if (!hasXp && badgeCount is null && friendCount is null)
        {
            return null;
        }

        double? progress = null;
        if (badges?.PlayerXp is int xp
            && badges.PlayerXpNeededCurrentLevel is int floorXp
            && badges.PlayerXpNeededToLevelUp is int neededToUp)
        {
            var earnedInLevel = Math.Max(0, xp - floorXp);
            var span = earnedInLevel + Math.Max(0, neededToUp);
            if (span > 0)
            {
                progress = Math.Clamp(Math.Round((double)earnedInLevel / span * 100.0, 1), 0, 100);
            }
        }

        return new AccountProgression(
            PlayerXp: badges?.PlayerXp,
            XpNeededToLevelUp: badges?.PlayerXpNeededToLevelUp,
            XpNeededCurrentLevel: badges?.PlayerXpNeededCurrentLevel,
            BadgeCount: badgeCount,
            FriendCount: friendCount,
            LevelProgressPercent: progress
        );
    }

    private void CacheProfile(string queryCacheKey, SteamProfileResponse profile)
    {
        var ttl = TimeSpan.FromMinutes(Math.Max(1, _options.CacheDurationMinutes));
        _cache.Set(queryCacheKey, profile, ttl);
        _cache.Set(BuildSteamIdCacheKey(profile.SteamId), profile, ttl);
    }

    private void CacheAlias(string queryCacheKey, SteamProfileResponse profile)
    {
        var ttl = TimeSpan.FromMinutes(Math.Max(1, _options.CacheDurationMinutes));
        _cache.Set(queryCacheKey, profile, ttl);
    }

    private static string BuildQueryCacheKey(SteamInputParseResult parsed) =>
        $"steam:query:{parsed.Kind}:{parsed.Value.ToLowerInvariant()}";

    private static string BuildSteamIdCacheKey(string steamId) =>
        $"steam:profile:{steamId}";

    private static SteamGameSummary MapGameSummary(SteamGameDto dto)
    {
        var name = string.IsNullOrWhiteSpace(dto.Name) ? $"App {dto.AppId}" : dto.Name;
        var playtimeHours = Math.Round(dto.PlaytimeForever / 60.0, 1);
        double? recentHours = dto.Playtime2Weeks is > 0
            ? Math.Round(dto.Playtime2Weeks.Value / 60.0, 1)
            : null;

        return new SteamGameSummary(
            AppId: dto.AppId,
            Name: name,
            PlaytimeHours: playtimeHours,
            RecentPlaytimeHours: recentHours,
            HeaderImageUrl: $"https://cdn.cloudflare.steamstatic.com/steam/apps/{dto.AppId}/header.jpg",
            StoreUrl: $"https://store.steampowered.com/app/{dto.AppId}"
        );
    }

    private static (string StatusText, string StatusKind) MapPersonaStatus(SteamPlayerDto player)
    {
        if (!string.IsNullOrWhiteSpace(player.GameExtraInfo))
        {
            return ("In-Game", "ingame");
        }

        return player.PersonaState switch
        {
            1 => ("Online", "online"),
            2 => ("Busy", "away"),
            3 => ("Away", "away"),
            4 => ("Snooze", "away"),
            5 => ("Looking to Trade", "online"),
            6 => ("Looking to Play", "online"),
            _ => ("Offline", "offline")
        };
    }
}
