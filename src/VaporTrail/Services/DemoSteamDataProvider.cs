using VaporTrail.Models;

namespace VaporTrail.Services;

public static class DemoSteamDataProvider
{
    public static IReadOnlyList<SampleProfileHint> GetSampleHints(bool isDemoMode)
    {
        if (isDemoMode)
        {
            return
            [
                new SampleProfileHint("GordonFreeman (in-game)", "gordonfreeman", "Public profile actively in-game"),
                new SampleProfileHint("Gabe Newell (SteamID64)", "76561197960435530", "Lookup by 17-digit SteamID64"),
                new SampleProfileHint("Private profile", "privateplayer", "Account with a private game library")
            ];
        }

        return
        [
            new SampleProfileHint("Gabe Newell", "76561197960435530", "SteamID64 lookup"),
            new SampleProfileHint("Vanity URL example", "https://steamcommunity.com/id/gabelogannewell", "Full Steam profile URL lookup")
        ];
    }

    public static SteamLookupResult ResolveDemoProfile(SteamInputParseResult parsed, DateTimeOffset now)
    {
        var key = parsed.Value.ToLowerInvariant();

        if (key is "notfound" or "unknown" or "404" or "missing_user")
        {
            return SteamLookupResult.Fail(
                404,
                $"No Steam account was found matching '{parsed.Value}'.");
        }

        if (key is "76561197960435530" or "gabelogannewell" or "gaben")
        {
            return SteamLookupResult.Ok(BuildGabeProfile(now));
        }

        if (key is "76561198000000001" or "gordonfreeman" or "freeman")
        {
            return SteamLookupResult.Ok(BuildGordonProfile(now));
        }

        if (key is "76561198000000002" or "privateplayer" or "private")
        {
            return SteamLookupResult.Ok(BuildPrivateProfile(now));
        }

        // Synthesize a realistic public demo profile for any other valid SteamID64 or vanity name.
        var steamId = parsed.Kind == SteamInputKind.SteamId64
            ? parsed.Value
            : DeriveDemoSteamId(parsed.Value);

        var displayName = parsed.Kind == SteamInputKind.VanityName
            ? parsed.Value
            : $"SteamPlayer_{parsed.Value[^4..]}";

        var games = new List<SteamGameSummary>
        {
            Game(730, "Counter-Strike 2", 412.6, 11.4),
            Game(1091500, "Cyberpunk 2077", 184.2, 6.8),
            Game(1245620, "ELDEN RING", 162.5, 4.2),
            Game(1145360, "Hades", 96.3, null),
            Game(413150, "Stardew Valley", 88.7, null),
            Game(620, "Portal 2", 42.1, null)
        };

        var profile = new SteamProfileResponse(
            SteamId: steamId,
            PersonaName: displayName,
            RealName: null,
            AvatarUrl: BuildAvatarDataUri(displayName, "#1d4ed8"),
            ProfileUrl: $"https://steamcommunity.com/profiles/{steamId}/",
            PersonaStateCode: 1,
            StatusText: "Online",
            StatusKind: "online",
            CurrentGameName: null,
            CurrentGameId: null,
            CountryCode: "US",
            MemberSinceUtc: new DateTimeOffset(2016, 4, 18, 12, 0, 0, TimeSpan.Zero),
            LastLogoffUtc: now.AddMinutes(-14),
            IsPublic: true,
            SteamLevel: 31,
            GamesArePrivate: false,
            TotalOwnedGames: 74,
            TotalPlaytimeHours: 1128.4,
            RecentlyPlayed: games.Where(g => g.RecentPlaytimeHours is > 0).ToList(),
            TopPlayedGames: games,
            IsDemoData: true,
            FromCache: false,
            FetchedAtUtc: now,
            Standing: CleanStanding(),
            Progression: new AccountProgression(
                PlayerXp: 6420,
                XpNeededToLevelUp: 180,
                XpNeededCurrentLevel: 6200,
                BadgeCount: 12,
                FriendCount: 38,
                LevelProgressPercent: 55.0
            )
        );

        return SteamLookupResult.Ok(profile);
    }

    public static IReadOnlyList<(int AppId, double PlaytimeHours)> GetDemoLibraryForBreakdown(SteamProfileResponse profile)
    {
        var baseList = profile.TopPlayedGames
            .Select(g => (g.AppId, g.PlaytimeHours))
            .ToList();

        if (profile.SteamId == "76561198000000001")
        {
            baseList.AddRange([
                (1145360, 74.0),
                (1091500, 52.0),
                (570, 38.0)
            ]);
        }
        else if (profile.SteamId == "76561197960435530")
        {
            baseList.AddRange([
                (413150, 96.0),
                (1145360, 64.0),
                (4000, 51.0)
            ]);
        }

        return baseList
            .GroupBy(x => x.AppId)
            .Select(g => (g.Key, g.First().PlaytimeHours))
            .ToList();
    }

    private static AccountStanding CleanStanding() =>
        new(
            VacBanned: false,
            VacBanCount: 0,
            GameBanCount: 0,
            DaysSinceLastBan: null,
            CommunityBanned: false,
            EconomyBan: "none",
            IsGoodStanding: true
        );

    private static SteamProfileResponse BuildGordonProfile(DateTimeOffset now)
    {
        var topGames = new List<SteamGameSummary>
        {
            Game(220, "Half-Life 2", 312.4, 8.2),
            Game(620, "Portal 2", 198.0, 3.5),
            Game(413150, "Stardew Valley", 164.5, 1.1),
            Game(546560, "Half-Life: Alyx", 121.8, 2.4),
            Game(4000, "Garry's Mod", 109.2, null),
            Game(70, "Half-Life", 84.6, null)
        };

        return new SteamProfileResponse(
            SteamId: "76561198000000001",
            PersonaName: "GordonFreeman",
            RealName: "Dr. Gordon Freeman",
            AvatarUrl: BuildAvatarDataUri("GF", "#1e3a8a"),
            ProfileUrl: "https://steamcommunity.com/profiles/76561198000000001/",
            PersonaStateCode: 1,
            StatusText: "In-Game",
            StatusKind: "ingame",
            CurrentGameName: "Half-Life 2",
            CurrentGameId: "220",
            CountryCode: "US",
            MemberSinceUtc: new DateTimeOffset(2003, 9, 12, 16, 30, 0, TimeSpan.Zero),
            LastLogoffUtc: now.AddHours(-2),
            IsPublic: true,
            SteamLevel: 42,
            GamesArePrivate: false,
            TotalOwnedGames: 148,
            TotalPlaytimeHours: 1420.5,
            RecentlyPlayed: topGames.Where(g => g.RecentPlaytimeHours is > 0).ToList(),
            TopPlayedGames: topGames,
            IsDemoData: true,
            FromCache: false,
            FetchedAtUtc: now,
            Standing: CleanStanding(),
            Progression: new AccountProgression(
                PlayerXp: 11420,
                XpNeededToLevelUp: 380,
                XpNeededCurrentLevel: 11000,
                BadgeCount: 18,
                FriendCount: 64,
                LevelProgressPercent: 52.5
            )
        );
    }

    private static SteamProfileResponse BuildGabeProfile(DateTimeOffset now)
    {
        var topGames = new List<SteamGameSummary>
        {
            Game(570, "Dota 2", 2410.8, 14.5),
            Game(730, "Counter-Strike 2", 640.2, 4.1),
            Game(440, "Team Fortress 2", 422.5, null),
            Game(550, "Left 4 Dead 2", 215.4, 1.8),
            Game(220, "Half-Life 2", 188.0, null),
            Game(620, "Portal 2", 142.3, null)
        };

        return new SteamProfileResponse(
            SteamId: "76561197960435530",
            PersonaName: "Rabscuttle",
            RealName: "Gabe Newell",
            AvatarUrl: BuildAvatarDataUri("GN", "#1e293b"),
            ProfileUrl: "https://steamcommunity.com/id/gabelogannewell/",
            PersonaStateCode: 1,
            StatusText: "Online",
            StatusKind: "online",
            CurrentGameName: null,
            CurrentGameId: null,
            CountryCode: "US",
            MemberSinceUtc: new DateTimeOffset(2003, 9, 12, 0, 0, 0, TimeSpan.Zero),
            LastLogoffUtc: now.AddMinutes(-27),
            IsPublic: true,
            SteamLevel: 58,
            GamesArePrivate: false,
            TotalOwnedGames: 312,
            TotalPlaytimeHours: 5184.2,
            RecentlyPlayed: topGames.Where(g => g.RecentPlaytimeHours is > 0).ToList(),
            TopPlayedGames: topGames,
            IsDemoData: true,
            FromCache: false,
            FetchedAtUtc: now,
            Standing: CleanStanding(),
            Progression: new AccountProgression(
                PlayerXp: 20150,
                XpNeededToLevelUp: 450,
                XpNeededCurrentLevel: 19800,
                BadgeCount: 34,
                FriendCount: 142,
                LevelProgressPercent: 43.8
            )
        );
    }

    private static SteamProfileResponse BuildPrivateProfile(DateTimeOffset now)
    {
        return new SteamProfileResponse(
            SteamId: "76561198000000002",
            PersonaName: "StealthRunner",
            RealName: null,
            AvatarUrl: BuildAvatarDataUri("SR", "#334155"),
            ProfileUrl: "https://steamcommunity.com/profiles/76561198000000002/",
            PersonaStateCode: 0,
            StatusText: "Offline",
            StatusKind: "offline",
            CurrentGameName: null,
            CurrentGameId: null,
            CountryCode: null,
            MemberSinceUtc: null,
            LastLogoffUtc: now.AddDays(-5),
            IsPublic: false,
            SteamLevel: null,
            GamesArePrivate: true,
            TotalOwnedGames: null,
            TotalPlaytimeHours: null,
            RecentlyPlayed: [],
            TopPlayedGames: [],
            IsDemoData: true,
            FromCache: false,
            FetchedAtUtc: now,
            Standing: CleanStanding(),
            Progression: null,
            Breakdown: null
        );
    }

    private static SteamGameSummary Game(int appId, string name, double hours, double? recentHours) =>
        new(
            AppId: appId,
            Name: name,
            PlaytimeHours: hours,
            RecentPlaytimeHours: recentHours,
            HeaderImageUrl: $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg",
            StoreUrl: $"https://store.steampowered.com/app/{appId}"
        );

    private static string DeriveDemoSteamId(string vanity)
    {
        uint hash = 2166136261;
        foreach (var ch in vanity.ToLowerInvariant())
        {
            hash ^= ch;
            hash *= 16777619;
        }

        ulong id = 76561198000000000UL + (hash % 900000000UL);
        return id.ToString();
    }

    private static string BuildAvatarDataUri(string label, string fillHex)
    {
        var initials = new string(label.Where(char.IsLetterOrDigit).Take(2).ToArray()).ToUpperInvariant();
        if (string.IsNullOrEmpty(initials))
        {
            initials = "ST";
        }

        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128">
              <rect width="128" height="128" rx="18" fill="{fillHex}"/>
              <text x="64" y="76" text-anchor="middle" fill="#f8fafc" font-family="system-ui,-apple-system,sans-serif" font-size="44" font-weight="700" letter-spacing="2">{initials}</text>
            </svg>
            """;

        return "data:image/svg+xml;utf8," + Uri.EscapeDataString(svg);
    }
}
