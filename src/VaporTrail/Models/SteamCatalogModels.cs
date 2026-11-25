using System.Text.Json.Serialization;

namespace VaporTrail.Models;

public sealed record SteamCatalogGame(
    int AppId,
    string Name,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Developers,
    IReadOnlyList<string> Publishers,
    string? ReleaseDate,
    int? PriceUsdCents,
    int? PriceFinalUsdCents,
    int? SteamReviewsPercent,
    int? MetacriticScore,
    double? HltbMainHours,
    double? AveragePlaytimeHours
);

public sealed record SteamCatalogStatus(
    bool IsReady,
    bool IsSyncing,
    int GameCount,
    DateTimeOffset? SyncedAtUtc,
    string? LastError
);

/// <summary>
/// Streaming projection of rows from leinstay/steamdb's steamdb.min.json.gz.
/// Unneeded heavy fields (HTML description, languages, voiceovers) are skipped during deserialization.
/// </summary>
public sealed class SteamDbRawEntry
{
    [JsonPropertyName("steam_appid")]
    public int? SteamAppId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("genres")]
    public List<string>? Genres { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("developers")]
    public List<string>? Developers { get; set; }

    [JsonPropertyName("publishers")]
    public List<string>? Publishers { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("price_usd")]
    public int? PriceUsd { get; set; }

    [JsonPropertyName("price_final_usd")]
    public int? PriceFinalUsd { get; set; }

    [JsonPropertyName("steam_reviews_percent")]
    public int? SteamReviewsPercent { get; set; }

    [JsonPropertyName("metacritic_score")]
    public int? MetacriticScore { get; set; }

    [JsonPropertyName("hltb_main_hours")]
    public double? HltbMainHours { get; set; }

    [JsonPropertyName("average_playtime_hours")]
    public double? AveragePlaytimeHours { get; set; }
}
