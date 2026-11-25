using VaporTrail.Models;

namespace VaporTrail.Services;

/// <summary>
/// Built-in metadata for demo/sample Steam apps so Demo Mode and offline tests produce rich genre,
/// tag, and developer breakdowns immediately before or without a remote catalog sync.
/// </summary>
public static class DemoSteamCatalogSeed
{
    private static readonly Dictionary<int, SteamCatalogGame> Games = new()
    {
        [70] = Game(70, "Half-Life", ["Action"], ["FPS", "Sci-Fi", "Classic", "Singleplayer", "Story Rich"], ["Valve"], "1998-11-19", 999, 96, 96, 12.0),
        [220] = Game(220, "Half-Life 2", ["Action"], ["FPS", "Sci-Fi", "Story Rich", "Classic", "Singleplayer"], ["Valve"], "2004-11-16", 999, 97, 96, 13.0),
        [440] = Game(440, "Team Fortress 2", ["Action", "Free To Play"], ["FPS", "Hero Shooter", "Multiplayer", "Team-Based", "Competitive"], ["Valve"], "2007-10-10", 0, 89, 92, null),
        [550] = Game(550, "Left 4 Dead 2", ["Action"], ["Co-op", "Zombies", "FPS", "Multiplayer", "Survival"], ["Valve"], "2009-11-17", 999, 97, 89, 10.0),
        [570] = Game(570, "Dota 2", ["Action", "Strategy", "Free To Play"], ["MOBA", "Multiplayer", "Competitive", "Strategy", "Team-Based"], ["Valve"], "2013-07-09", 0, 81, 90, null),
        [620] = Game(620, "Portal 2", ["Action", "Adventure"], ["Puzzle", "Co-op", "Story Rich", "Sci-Fi", "Comedy"], ["Valve"], "2011-04-19", 999, 98, 95, 8.5),
        [730] = Game(730, "Counter-Strike 2", ["Action", "Free To Play"], ["FPS", "Tactical", "Competitive", "Multiplayer", "Team-Based"], ["Valve"], "2012-08-21", 0, 87, 83, null),
        [4000] = Game(4000, "Garry's Mod", ["Casual", "Indie", "Simulation"], ["Sandbox", "Multiplayer", "Moddable", "Physics", "Funny"], ["Facepunch Studios"], "2006-11-29", 999, 96, null, null),
        [413150] = Game(413150, "Stardew Valley", ["Indie", "RPG", "Simulation"], ["Farming Sim", "Pixel Graphics", "Relaxing", "Co-op", "Singleplayer"], ["ConcernedApe"], "2016-02-26", 1499, 98, 89, 53.0),
        [546560] = Game(546560, "Half-Life: Alyx", ["Action", "Adventure"], ["VR", "FPS", "Sci-Fi", "Story Rich", "Horror"], ["Valve"], "2020-03-23", 5999, 98, 93, 12.0),
        [1091500] = Game(1091500, "Cyberpunk 2077", ["RPG"], ["Open World", "Sci-Fi", "Story Rich", "Cyberpunk", "Singleplayer"], ["CD PROJEKT RED"], "2020-12-10", 5999, 85, 86, 25.5),
        [1145360] = Game(1145360, "Hades", ["Action", "Indie", "RPG"], ["Roguelike", "Hack and Slash", "Story Rich", "Isometric", "Singleplayer"], ["Supergiant Games"], "2020-09-17", 2499, 98, 93, 22.5),
        [1245620] = Game(1245620, "ELDEN RING", ["Action", "RPG"], ["Souls-like", "Open World", "Dark Fantasy", "Difficult", "Singleplayer"], ["FromSoftware, Inc."], "2022-02-25", 5999, 92, 94, 59.5)
    };

    public static bool TryGet(int appId, out SteamCatalogGame game) =>
        Games.TryGetValue(appId, out game!);

    private static SteamCatalogGame Game(
        int appId,
        string name,
        string[] genres,
        string[] tags,
        string[] developers,
        string releaseDate,
        int priceUsdCents,
        int reviewsPercent,
        int? metacritic,
        double? hltbMain) =>
        new(
            AppId: appId,
            Name: name,
            Genres: genres,
            Tags: tags,
            Developers: developers,
            Publishers: developers,
            ReleaseDate: releaseDate,
            PriceUsdCents: priceUsdCents,
            PriceFinalUsdCents: priceUsdCents,
            SteamReviewsPercent: reviewsPercent,
            MetacriticScore: metacritic,
            HltbMainHours: hltbMain,
            AveragePlaytimeHours: null
        );
}
