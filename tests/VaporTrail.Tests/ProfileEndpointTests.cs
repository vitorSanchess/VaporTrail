using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using VaporTrail.Models;

namespace VaporTrail.Tests;

public class ProfileEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RootPage_ReturnsHtmlFrontend()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("VaporTrail", html);
        Assert.Contains("theme-toggle", html);
    }

    [Fact]
    public async Task StatusEndpoint_ReturnsServiceMetadata()
    {
        var response = await _client.GetAsync("/api/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<SteamServiceStatus>();
        Assert.NotNull(status);
        Assert.NotEmpty(status.SampleProfiles);
    }

    [Fact]
    public async Task ProfileEndpoint_ValidDemoQuery_ReturnsProfile()
    {
        var response = await _client.GetAsync("/api/steam/profile?query=gordonfreeman");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<SteamProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal("GordonFreeman", profile.PersonaName);
        Assert.True(profile.IsDemoData);
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
}
