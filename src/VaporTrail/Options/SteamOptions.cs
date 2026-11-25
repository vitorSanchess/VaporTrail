namespace VaporTrail.Options;

public sealed class SteamOptions
{
    public const string SectionName = "Steam";

    /// <summary>
    /// Steam Web API key obtained from https://steamcommunity.com/dev/apikey.
    /// Leave blank to run in Demo Mode with built-in sample profiles.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base URL for the Steam Web API.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.steampowered.com";

    /// <summary>
    /// How long resolved profiles are cached in memory (in minutes).
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 5;

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
}
