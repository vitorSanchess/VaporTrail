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
    DateTimeOffset FetchedAtUtc,
    AccountStanding? Standing = null,
    AccountProgression? Progression = null,
    LibraryBreakdown? Breakdown = null
);

public sealed record AccountStanding(
    bool VacBanned,
    int VacBanCount,
    int GameBanCount,
    int? DaysSinceLastBan,
    bool CommunityBanned,
    string EconomyBan,
    bool IsGoodStanding
);

public sealed record AccountProgression(
    int? PlayerXp,
    int? XpNeededToLevelUp,
    int? XpNeededCurrentLevel,
    int? BadgeCount,
    int? FriendCount,
    double? LevelProgressPercent
);

public sealed record BreakdownSlice(
    string Name,
    double PlaytimeHours,
    int GameCount,
    double SharePercent
);

public sealed record LibraryBreakdown(
    int AnalyzedGamesCount,
    int MatchedGamesCount,
    IReadOnlyList<BreakdownSlice> GenresByPlaytime,
    IReadOnlyList<BreakdownSlice> GenresByCount,
    IReadOnlyList<BreakdownSlice> TopTags,
    IReadOnlyList<BreakdownSlice> TopDevelopers,
    IReadOnlyList<BreakdownSlice>? TopTagsByCount = null,
    IReadOnlyList<BreakdownSlice>? TopDevelopersByCount = null
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
    IReadOnlyList<SampleProfileHint> SampleProfiles,
    SteamCatalogStatus? Catalog = null
);
