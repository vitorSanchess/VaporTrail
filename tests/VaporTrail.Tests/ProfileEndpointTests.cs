using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using VaporTrail.Models;

namespace VaporTrail.Tests;

public class ProfileEndpointTests(ProfileEndpointTests.VaporTrailAppFactory factory)
    : IClassFixture<ProfileEndpointTests.VaporTrailAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    public sealed class VaporTrailAppFactory : WebApplicationFactory<Program>
    {
        private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"vaportrail-web-{Guid.NewGuid():N}");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(_tempDir);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Steam:ApiKey"] = "",
                    ["STEAM_API_KEY"] = "",
                    ["Catalog:AutoSyncOnStartup"] = "false",
                    ["Catalog:DatabasePath"] = Path.Combine(_tempDir, "test-catalog.sqlite")
                });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RootPage_ReturnsHtmlFrontend()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("VaporTrail", html);
        Assert.Contains("theme-toggle", html);
        Assert.Contains("genre-donut", html);
    }

    [Fact]
    public async Task StatusAndCatalogEndpoints_ReturnMetadata()
    {
        var statusResponse = await _client.GetAsync("/api/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var status = await statusResponse.Content.ReadFromJsonAsync<SteamServiceStatus>();
        Assert.NotNull(status);
        Assert.NotEmpty(status.SampleProfiles);
        Assert.NotNull(status.Catalog);

        var catalogResponse = await _client.GetAsync("/api/catalog/status");
        Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);
        var catalogStatus = await catalogResponse.Content.ReadFromJsonAsync<SteamCatalogStatus>();
        Assert.NotNull(catalogStatus);
    }

    [Fact]
    public async Task ProfileEndpoint_ValidDemoQuery_ReturnsProfileWithStandingAndBreakdown()
    {
        var response = await _client.GetAsync("/api/steam/profile?query=gordonfreeman");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<SteamProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal("GordonFreeman", profile.PersonaName);
        Assert.True(profile.IsDemoData);
        Assert.NotNull(profile.Standing);
        Assert.True(profile.Standing.IsGoodStanding);
        Assert.NotNull(profile.Progression);
        Assert.NotNull(profile.Breakdown);
        Assert.NotEmpty(profile.Breakdown.GenresByPlaytime);
        Assert.NotEmpty(profile.Breakdown.TopDevelopers);
        Assert.NotEmpty(profile.Breakdown.TopTags);
        Assert.NotNull(profile.Engagement);
        Assert.True(profile.Engagement.UnplayedGamesCount > 0);
        Assert.True(profile.Engagement.EstimatedBacklogHours > 0);
        Assert.Equal(4, profile.Engagement.DepthBuckets.Count);
        Assert.NotEmpty(profile.Engagement.QuickestToBeat);
    }

    [Fact]
    public async Task ProfileEndpoint_EmptyQuery_Returns400()
    {
        var response = await _client.GetAsync("/api/steam/profile?query=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProfileEndpoint_NotFoundDemoQuery_Returns404()
    {
        var response = await _client.GetAsync("/api/steam/profile?query=notfound");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProductionEnvironment_DisablesManualCatalogSync()
    {
        using var prodFactory = factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));
        using var prodClient = prodFactory.CreateClient();

        var statusResponse = await prodClient.GetAsync("/api/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var status = await statusResponse.Content.ReadFromJsonAsync<SteamServiceStatus>();
        Assert.NotNull(status);
        Assert.False(status.AllowManualCatalogSync);

        var syncResponse = await prodClient.PostAsync("/api/catalog/sync?force=true", null);
        Assert.Equal(HttpStatusCode.NotFound, syncResponse.StatusCode);
    }
}
