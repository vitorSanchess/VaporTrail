using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        Assert.Equal(4, requestCount);

        // Second request should hit memory cache without calling Steam API.
        var second = await service.GetProfileAsync(TestSteamId);
        Assert.True(second.IsSuccess);
        Assert.True(second.Profile!.FromCache);
        Assert.Equal(4, requestCount);

        // Refresh should bypass cache and fetch fresh data.
        var refreshed = await service.GetProfileAsync(TestSteamId, bypassCache: true);
        Assert.True(refreshed.IsSuccess);
        Assert.False(refreshed.Profile!.FromCache);
        Assert.Equal(8, requestCount);
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

        return new SteamProfileService(httpClient, cache, options, NullLogger<SteamProfileService>.Instance);
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
