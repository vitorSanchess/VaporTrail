using Microsoft.Extensions.Options;
using VaporTrail.Options;
using VaporTrail.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SteamOptions>(options =>
{
    builder.Configuration.GetSection(SteamOptions.SectionName).Bind(options);

    // Also accept the common STEAM_API_KEY environment variable as a convenience fallback.
    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        var envKey = builder.Configuration["STEAM_API_KEY"];
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            options.ApiKey = envKey;
        }
    }
});

builder.Services.Configure<CatalogOptions>(
    builder.Configuration.GetSection(CatalogOptions.SectionName));

builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();

builder.Services.AddHttpClient("SteamCatalog", client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("VaporTrail/1.0");
});

builder.Services.AddSingleton<ISteamCatalogService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("SteamCatalog");
    var options = sp.GetRequiredService<IOptions<CatalogOptions>>();
    var env = sp.GetRequiredService<IHostEnvironment>();
    var logger = sp.GetRequiredService<ILogger<SqliteSteamCatalogService>>();
    return new SqliteSteamCatalogService(httpClient, options, env, logger);
});

builder.Services.AddHostedService<SteamCatalogSyncHostedService>();

builder.Services.AddHttpClient<ISteamProfileService, SteamProfileService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<SteamOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
        ? "https://api.steampowered.com/"
        : options.BaseUrl.TrimEnd('/') + "/";

    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("VaporTrail/1.0");
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapGet("/health", () => Results.Ok(new { status = "ok" }));

api.MapGet("/status", (ISteamProfileService steamService) =>
    Results.Ok(steamService.GetStatus()));

api.MapGet("/catalog/status", (ISteamCatalogService catalogService) =>
    Results.Ok(catalogService.GetStatus()));

api.MapPost("/catalog/sync", async (
    bool? force,
    ISteamCatalogService catalogService,
    CancellationToken cancellationToken) =>
{
    var status = await catalogService.SyncFromRemoteAsync(force ?? false, cancellationToken);
    return Results.Ok(status);
});

api.MapGet("/steam/profile", async (
    string? query,
    bool? refresh,
    ISteamProfileService steamService,
    CancellationToken cancellationToken) =>
{
    var result = await steamService.GetProfileAsync(query, refresh ?? false, cancellationToken);

    if (result.IsSuccess && result.Profile is not null)
    {
        return Results.Ok(result.Profile);
    }

    return Results.Problem(
        title: result.StatusCode switch
        {
            400 => "Invalid Steam Identifier",
            404 => "Profile Not Found",
            _ => "Steam API Unavailable"
        },
        detail: result.ErrorMessage,
        statusCode: result.StatusCode);
});

app.Run();

public partial class Program;
