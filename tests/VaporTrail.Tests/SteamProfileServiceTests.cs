using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VaporTrail.Models;
using VaporTrail.Options;
using VaporTrail.Services;

namespace VaporTrail.Tests;

public class SteamProfileServiceTests
{
    private const string TestApiKey = "TEST_STEAM_API_KEY";
    private const string TestSteamId = "76561197960435530";

    [Fact]
    public async Task GetProfileAsync_PublicProfile_AggregatesSummaryLevelAndGamesAndCaches()
    {
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(req =>
        {
            Interlocked.Increment(ref requestCount);
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("GetPlayerSummaries"))
            {
                return JsonResponse("""
                {
                  "response": {
                    "players": [{
                      "steamid": "76561197960435530",
                      "communityvisibilitystate": 3,
                      "personaname": "GordonFreeman",
                      "realname": "Gordon Freeman",
                      "profileurl": "https://steamcommunity.com/id/gordon/",
                      "avatarfull": "https://avatars.steamstatic.com/test_full.jpg",
                      "personastate": 1,
                      "timecreated": 1063324800,
                      "lastlogoff": 1700000000,
                      "loccountrycode": "US",
                      "gameextrainfo": "Half-Life 2",
                      "gameid": "220"
                    }]
                  }
                }
                """);
            }

            if (path.Contains("GetSteamLevel"))
            {
                return JsonResponse("""{ "response": { "player_level": 42 } }""");
            }

            if (path.Contains("GetRecentlyPlayedGames"))
            {
                return JsonResponse("""
                {
                  "response": {
                    "total_count": 1,
                    "games": [
                      { "appid": 220, "name": "Half-Life 2", "playtime_2weeks": 120, "playtime_forever": 6000 }
                    ]
                  }
                }
                """);
            }

            if (path.Contains("GetOwnedGames"))
            {
                return JsonResponse("""
                {
                  "response": {
                    "game_count": 2,
                    "games": [
                      { "appid": 620, "name": "Portal 2", "playtime_forever": 1800 },
                      { "appid": 220, "name": "Half-Life 2", "playtime_forever": 6000, "playtime_2weeks": 120 }
                    ]
                  }
                }
                """);
            }

            if (path.Contains("GetBadges"))
            {
                return JsonResponse("""
                {
                  "response": {
                    "player_xp": 11420,
                    "player_level": 42,
                    "player_xp_needed_to_level_up": 380,
                    "player_xp_needed_current_level": 11000,
                    "badges": [
                      { "badgeid": 1, "level": 5, "xp": 500 },
                      { "badgeid": 2, "level": 1, "xp": 100 }
                    ]
                  }
                }
                """);
            }

            if (path.Contains("GetPlayerBans"))
            {
                return JsonResponse("""
                {
                  "players": [{
                    "SteamId": "76561197960435530",
                    "CommunityBanned": false,
                    "VACBanned": false,
                    "NumberOfVACBans": 0,
                    "DaysSinceLastBan": 0,
                    "NumberOfGameBans": 0,
                    "EconomyBan": "none"
                  }]
                }
                """);
            }

            if (path.Contains("GetFriendList"))
            {
                return JsonResponse("""
                {
                  "friendslist": {
                    "friends": [
                      { "steamid": "76561198000000001" },
                      { "steamid": "76561198000000002" }
                    ]
                  }
                }
                """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = CreateService(handler, apiKey: TestApiKey);

        var first = await service.GetProfileAsync(TestSteamId);
        Assert.True(first.IsSuccess);
        Assert.NotNull(first.Profile);
        Assert.Equal("GordonFreeman", first.Profile.PersonaName);
        Assert.Equal("In-Game", first.Profile.StatusText);
        Assert.Equal("ingame", first.Profile.StatusKind);
        Assert.Equal("Half-Life 2", first.Profile.CurrentGameName);
        Assert.Equal(42, first.Profile.SteamLevel);
        Assert.False(first.Profile.GamesArePrivate);
        Assert.Equal(2, first.Profile.TotalOwnedGames);
        Assert.Equal(130.0, first.Profile.TotalPlaytimeHours);
        Assert.Equal("Half-Life 2", first.Profile.TopPlayedGames[0].Name);
        Assert.Single(first.Profile.RecentlyPlayed);
        Assert.False(first.Profile.FromCache);
        Assert.False(first.Profile.IsDemoData);
        Assert.NotNull(first.Profile.Standing);
        Assert.True(first.Profile.Standing.IsGoodStanding);
        Assert.NotNull(first.Profile.Progression);
        Assert.Equal(11420, first.Profile.Progression.PlayerXp);
        Assert.Equal(2, first.Profile.Progression.BadgeCount);
        Assert.Equal(2, first.Profile.Progression.FriendCount);
        Assert.Equal(52.5, first.Profile.Progression.LevelProgressPercent);
        Assert.NotNull(first.Profile.Breakdown);
        Assert.Equal(2, first.Profile.Breakdown.MatchedGamesCount);
        Assert.Equal("Action", first.Profile.Breakdown.GenresByPlaytime[0].Name);
        Assert.Equal("Valve", first.Profile.Breakdown.TopDevelopers[0].Name);
        Assert.Equal(7, requestCount);

        // Second request should hit memory cache without calling Steam API.
        var second = await service.GetProfileAsync(TestSteamId);
        Assert.True(second.IsSuccess);
        Assert.True(second.Profile!.FromCache);
        Assert.Equal(7, requestCount);

        // Refresh should bypass cache and fetch fresh data.
        var refreshed = await service.GetProfileAsync(TestSteamId, bypassCache: true);
        Assert.True(refreshed.IsSuccess);
        Assert.False(refreshed.Profile!.FromCache);
        Assert.Equal(14, requestCount);
    }

    [Fact]
    public async Task GetProfileAsync_VanityName_ResolvesViaApi()
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("ResolveVanityURL"))
            {
                return JsonResponse("""{ "response": { "steamid": "76561197960435530", "success": 1 } }""");
            }

            if (path.Contains("GetPlayerSummaries"))
            {
                return JsonResponse("""
                {
                  "response": {
                    "players": [{
                      "steamid": "76561197960435530",
                      "communityvisibilitystate": 3,
                      "personaname": "GabeN",
                      "profileurl": "https://steamcommunity.com/id/gabelogannewell/",
                      "personastate": 1
                    }]
                  }
                }
                """);
            }

            return JsonResponse("""{ "response": {} }""");
        });

        var service = CreateService(handler, apiKey: TestApiKey);
        var result = await service.GetProfileAsync("https://steamcommunity.com/id/gabelogannewell");

        Assert.True(result.IsSuccess);
        Assert.Equal(TestSteamId, result.Profile!.SteamId);
        Assert.Equal("GabeN", result.Profile.PersonaName);
    }

    [Fact]
    public async Task GetProfileAsync_UnknownVanityName_Returns404()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse("""{ "response": { "success": 42, "message": "No match" } }"""));

        var service = CreateService(handler, apiKey: TestApiKey);
        var result = await service.GetProfileAsync("does_not_exist_12345");

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task GetProfileAsync_PrivateProfile_DegradesGracefullyWithoutError()
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("GetPlayerSummaries"))
            {
                return JsonResponse("""
                {
                  "response": {
                    "players": [{
                      "steamid": "76561198000000002",
                      "communityvisibilitystate": 1,
                      "personaname": "StealthRunner",
                      "profileurl": "https://steamcommunity.com/profiles/76561198000000002/",
                      "personastate": 0
                    }]
                  }
                }
                """);
            }

            // Steam returns empty response objects for private game details & level.
            return JsonResponse("""{ "response": {} }""");
        });

        var service = CreateService(handler, apiKey: TestApiKey);
        var result = await service.GetProfileAsync("76561198000000002");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Profile);
        Assert.False(result.Profile.IsPublic);
        Assert.True(result.Profile.GamesArePrivate);
        Assert.Null(result.Profile.SteamLevel);
        Assert.Null(result.Profile.TotalOwnedGames);
        Assert.Empty(result.Profile.TopPlayedGames);
        Assert.Empty(result.Profile.RecentlyPlayed);
    }

    [Fact]
    public async Task GetProfileAsync_NoApiKey_RunsInDemoMode()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Should not hit HTTP in Demo Mode"));
        var service = CreateService(handler, apiKey: null);

        var status = service.GetStatus();
        Assert.True(status.IsDemoMode);
        Assert.Equal("demo", status.Mode);

        var result = await service.GetProfileAsync("gordonfreeman");
        Assert.True(result.IsSuccess);
        Assert.True(result.Profile!.IsDemoData);
        Assert.Equal("GordonFreeman", result.Profile.PersonaName);
        Assert.NotNull(result.Profile.Standing);
        Assert.NotNull(result.Profile.Progression);
        Assert.NotNull(result.Profile.Breakdown);
        Assert.NotEmpty(result.Profile.Breakdown.GenresByPlaytime);
    }

    [Fact]
    public async Task GetProfileAsync_PrivateFriendListAndVacBan_DegradesGracefullyAndReportsBan()
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("GetPlayerSummaries"))
            {
                return JsonResponse("""
                {
                  "response": {
                    "players": [{
                      "steamid": "76561198000000099",
                      "communityvisibilitystate": 3,
                      "personaname": "FlaggedUser",
                      "profileurl": "https://steamcommunity.com/profiles/76561198000000099/",
                      "personastate": 1
                    }]
                  }
                }
                """);
            }

            if (path.Contains("GetPlayerBans"))
            {
                return JsonResponse("""
                {
                  "players": [{
                    "SteamId": "76561198000000099",
                    "CommunityBanned": false,
                    "VACBanned": true,
                    "NumberOfVACBans": 2,
                    "DaysSinceLastBan": 410,
                    "NumberOfGameBans": 1,
                    "EconomyBan": "none"
                  }]
                }
                """);
            }

            if (path.Contains("GetFriendList"))
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            return JsonResponse("""{ "response": {} }""");
        });

        var service = CreateService(handler, apiKey: TestApiKey);
        var result = await service.GetProfileAsync("76561198000000099");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Profile?.Standing);
        Assert.False(result.Profile.Standing.IsGoodStanding);
        Assert.True(result.Profile.Standing.VacBanned);
        Assert.Equal(2, result.Profile.Standing.VacBanCount);
        Assert.Equal(1, result.Profile.Standing.GameBanCount);
        Assert.Equal(410, result.Profile.Standing.DaysSinceLastBan);
        Assert.Null(result.Profile.Progression);
    }

    private static SteamProfileService CreateService(HttpMessageHandler handler, string? apiKey)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.steampowered.com/")
        };

        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Microsoft.Extensions.Options.Options.Create(new SteamOptions
        {
            ApiKey = apiKey,
            CacheDurationMinutes = 5
        });

        return new SteamProfileService(
            httpClient,
            cache,
            new SeedOnlyCatalogService(),
            options,
            NullLogger<SteamProfileService>.Instance);
    }

    private sealed class SeedOnlyCatalogService : ISteamCatalogService
    {
        public SteamCatalogStatus GetStatus() => new(false, false, 0, null, null);

        public Task<IReadOnlyDictionary<int, SteamCatalogGame>> GetGamesByAppIdsAsync(
            IEnumerable<int> appIds,
            CancellationToken cancellationToken = default)
        {
            var map = new Dictionary<int, SteamCatalogGame>();
            foreach (var id in appIds.Distinct())
            {
                if (DemoSteamCatalogSeed.TryGet(id, out var game))
                {
                    map[id] = game;
                }
            }
            return Task.FromResult<IReadOnlyDictionary<int, SteamCatalogGame>>(map);
        }

        public Task<SteamCatalogStatus> SyncFromRemoteAsync(bool force = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(GetStatus());

        public Task<int> SyncFromStreamAsync(Stream stream, bool isGzip, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
