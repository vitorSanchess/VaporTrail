using System.Text.RegularExpressions;

namespace VaporTrail.Services;

public enum SteamInputKind
{
    SteamId64,
    VanityName
}

public sealed record SteamInputParseResult(
    bool IsSuccess,
    SteamInputKind Kind,
    string Value,
    string? ErrorMessage
)
{
    public static SteamInputParseResult Success(SteamInputKind kind, string value) =>
        new(true, kind, value, null);

    public static SteamInputParseResult Failure(string errorMessage) =>
        new(false, default, string.Empty, errorMessage);
}

public static partial class SteamInputParser
{
    private const ulong MinIndividualSteamId64 = 76561197960265728UL;

    [GeneratedRegex(@"^\d{17}$", RegexOptions.Compiled)]
    private static partial Regex SteamId64Regex();

    [GeneratedRegex(@"^[a-zA-Z0-9_-]{2,64}$", RegexOptions.Compiled)]
    private static partial Regex VanityNameRegex();

    public static SteamInputParseResult Parse(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return SteamInputParseResult.Failure(
                "Please enter a 64-bit Steam ID, custom vanity name, or Steam profile URL.");
        }

        var trimmed = rawInput.Trim();

        // Check if input looks like a URL (with or without scheme).
        if (trimmed.Contains("steamcommunity.com", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return ParseSteamUrl(trimmed);
        }

        // Allow leading '@' for convenience (e.g., @gabelogannewell).
        if (trimmed.StartsWith('@'))
        {
            trimmed = trimmed[1..].Trim();
        }

        if (IsValidSteamId64(trimmed))
        {
            return SteamInputParseResult.Success(SteamInputKind.SteamId64, trimmed);
        }

        // All-digit input that isn't a valid 17-digit SteamID64.
        if (trimmed.All(char.IsDigit))
        {
            return SteamInputParseResult.Failure(
                "Numeric Steam IDs must be a valid 17-digit 64-bit SteamID (e.g., 76561197960435530).");
        }

        if (VanityNameRegex().IsMatch(trimmed))
        {
            return SteamInputParseResult.Success(SteamInputKind.VanityName, trimmed);
        }

        return SteamInputParseResult.Failure(
            "Invalid Steam identifier. Enter a 17-digit SteamID64, a custom vanity name (letters, numbers, _ or -), or a steamcommunity.com profile URL.");
    }

    private static SteamInputParseResult ParseSteamUrl(string input)
    {
        var withScheme = input.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? input
            : $"https://{input}";

        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out var uri))
        {
            return SteamInputParseResult.Failure("That does not appear to be a valid Steam profile URL.");
        }

        var host = uri.Host.ToLowerInvariant();
        if (host != "steamcommunity.com" && host != "www.steamcommunity.com")
        {
            return SteamInputParseResult.Failure(
                "Only steamcommunity.com profile URLs are supported (e.g., https://steamcommunity.com/id/name or /profiles/7656119...).");
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length < 2)
        {
            return SteamInputParseResult.Failure(
                "Steam profile URL must include /profiles/<steamid64> or /id/<vanityname>.");
        }

        var route = segments[0].ToLowerInvariant();
        var identifier = Uri.UnescapeDataString(segments[1]);

        if (route == "profiles")
        {
            return IsValidSteamId64(identifier)
                ? SteamInputParseResult.Success(SteamInputKind.SteamId64, identifier)
                : SteamInputParseResult.Failure(
                    "The /profiles/ URL does not contain a valid 17-digit 64-bit SteamID.");
        }

        if (route == "id")
        {
            return VanityNameRegex().IsMatch(identifier)
                ? SteamInputParseResult.Success(SteamInputKind.VanityName, identifier)
                : SteamInputParseResult.Failure(
                    "The /id/ URL does not contain a valid Steam custom vanity name.");
        }

        return SteamInputParseResult.Failure(
            "Steam profile URL must start with /profiles/ or /id/.");
    }

    public static bool IsValidSteamId64(string candidate)
    {
        if (!SteamId64Regex().IsMatch(candidate))
        {
            return false;
        }

        return ulong.TryParse(candidate, out var id) && id >= MinIndividualSteamId64;
    }
}
