namespace VaporTrail.Models;

public sealed record SteamProfileResponse(
    string SteamId,
    string PersonaName,
    string? RealName,
    string AvatarUrl,
    string ProfileUrl,
    int PersonaStateCode,
    string StatusText,
    string StatusKind,
    string? CurrentGameName,
    string? CurrentGameId,
    string? CountryCode,
    DateTimeOffset? MemberSinceUtc,
    DateTimeOffset? LastLogoffUtc,
    bool IsPublic,
    int? SteamLevel,
    bool GamesArePrivate,
    int? TotalOwnedGames,
    double? TotalPlaytimeHours,
    IReadOnlyList<SteamGameSummary> RecentlyPlayed,
    IReadOnlyList<SteamGameSummary> TopPlayedGames,
    bool IsDemoData,
    bool FromCache,
    DateTimeOffset FetchedAtUtc
);

public sealed record SteamGameSummary(
    int AppId,
    string Name,
    double PlaytimeHours,
    double? RecentPlaytimeHours,
    string HeaderImageUrl,
    string StoreUrl
);

public sealed record SampleProfileHint(
    string Label,
    string Query,
    string Description
);

public sealed record SteamServiceStatus(
    string Mode,
    bool IsDemoMode,
    int CacheDurationMinutes,
    IReadOnlyList<SampleProfileHint> SampleProfiles
);
