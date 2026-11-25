using VaporTrail.Models;

namespace VaporTrail.Services;

public sealed record SteamLookupResult(
    bool IsSuccess,
    int StatusCode,
    string? ErrorMessage,
    SteamProfileResponse? Profile
)
{
    public static SteamLookupResult Ok(SteamProfileResponse profile) =>
        new(true, 200, null, profile);

    public static SteamLookupResult Fail(int statusCode, string errorMessage) =>
        new(false, statusCode, errorMessage, null);
}

public interface ISteamProfileService
{
    SteamServiceStatus GetStatus();

    Task<SteamLookupResult> GetProfileAsync(
        string? rawQuery,
        bool bypassCache = false,
        CancellationToken cancellationToken = default);
}
