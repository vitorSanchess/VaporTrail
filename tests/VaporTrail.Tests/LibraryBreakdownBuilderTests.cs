using VaporTrail.Models;
using VaporTrail.Services;

namespace VaporTrail.Tests;

public class LibraryBreakdownBuilderTests
{
    [Fact]
    public void Build_RollsUpTop5GenresPlusOther_AndSortsTagsAndDevelopers()
    {
        var owned = new List<(int AppId, double PlaytimeHours)>
        {
            (1, 100),
            (2, 50),
            (3, 30),
            (4, 20),
            (5, 10),
            (6, 5),
            (7, 5)
        };

        var catalog = new Dictionary<int, SteamCatalogGame>
        {
            [1] = Meta(1, ["Action"], ["FPS", "Sci-Fi"], ["Studio A"]),
            [2] = Meta(2, ["RPG"], ["Story Rich", "Sci-Fi"], ["Studio B"]),
            [3] = Meta(3, ["Strategy"], ["Tactical"], ["Studio A"]),
            [4] = Meta(4, ["Indie"], ["Pixel Graphics"], ["Studio C"]),
            [5] = Meta(5, ["Simulation"], ["Sandbox"], ["Studio D"]),
            [6] = Meta(6, ["Adventure"], ["Puzzle"], ["Studio E"]),
            [7] = Meta(7, ["Racing"], ["Arcade"], ["Studio F"])
        };

        var breakdown = LibraryBreakdownBuilder.Build(owned, catalog);

        Assert.NotNull(breakdown);
        Assert.Equal(7, breakdown.AnalyzedGamesCount);
        Assert.Equal(7, breakdown.MatchedGamesCount);

        // Top 5 genres + "Other" rollup = 6 slices
        Assert.Equal(6, breakdown.GenresByPlaytime.Count);
        Assert.Equal("Action", breakdown.GenresByPlaytime[0].Name);
        Assert.Equal("Other", breakdown.GenresByPlaytime[^1].Name);
        Assert.Equal(10.0, breakdown.GenresByPlaytime[^1].PlaytimeHours);
        Assert.Equal(2, breakdown.GenresByPlaytime[^1].GameCount);

        var playtimeShareSum = breakdown.GenresByPlaytime.Sum(s => s.SharePercent);
        Assert.InRange(playtimeShareSum, 99.5, 100.5);

        Assert.Equal(6, breakdown.GenresByCount.Count);
        Assert.Equal("Other", breakdown.GenresByCount[^1].Name);
        Assert.Equal(2, breakdown.GenresByCount[^1].GameCount);

        // Top developer should be Studio A (100 + 30 = 130h, 2 games)
        Assert.Equal("Studio A", breakdown.TopDevelopers[0].Name);
        Assert.Equal(130.0, breakdown.TopDevelopers[0].PlaytimeHours);
        Assert.Equal(2, breakdown.TopDevelopers[0].GameCount);

        // Top tag should be Sci-Fi (100 + 50 = 150h, 2 games)
        Assert.Equal("Sci-Fi", breakdown.TopTags[0].Name);
        Assert.Equal(150.0, breakdown.TopTags[0].PlaytimeHours);
        Assert.Equal(2, breakdown.TopTags[0].GameCount);
    }

    [Fact]
    public void Build_ZeroPlaytimeLibrary_FallsBackToGameCounts()
    {
        var owned = new List<(int AppId, double PlaytimeHours)>
        {
            (1, 0),
            (2, 0)
        };

        var catalog = new Dictionary<int, SteamCatalogGame>
        {
            [1] = Meta(1, ["Action"], ["FPS"], ["Studio A"]),
            [2] = Meta(2, ["Action", "Indie"], ["Roguelike"], ["Studio B"])
        };

        var breakdown = LibraryBreakdownBuilder.Build(owned, catalog);

        Assert.NotNull(breakdown);
        Assert.Equal("Action", breakdown.GenresByPlaytime[0].Name);
        Assert.Equal(2, breakdown.GenresByPlaytime[0].GameCount);
        Assert.Equal(66.7, breakdown.GenresByPlaytime[0].SharePercent);
    }

    [Fact]
    public void Build_NormalizesAndDeduplicatesDeveloperAliases()
    {
        var owned = new List<(int AppId, double PlaytimeHours)>
        {
            (1, 100),
            (2, 50),
            (3, 40),
            (4, 30),
            (5, 20)
        };

        var catalog = new Dictionary<int, SteamCatalogGame>
        {
            [1] = Meta(1, ["Action"], ["FPS"], ["Valve", "Valve Corporation"]),
            [2] = Meta(2, ["Action"], ["Puzzle"], ["Valve Corporation"]),
            [3] = Meta(3, ["Indie"], ["Farming Sim"], ["ConcernedApe", "ConcernedApe LLC", "Eric Barone"]),
            [4] = Meta(4, ["Action"], ["Souls-like"], ["FromSoftware, Inc."]),
            [5] = Meta(5, ["Action"], ["Action"], ["FromSoftware"])
        };

        var breakdown = LibraryBreakdownBuilder.Build(owned, catalog);

        Assert.NotNull(breakdown);
        Assert.Equal(3, breakdown.TopDevelopers.Count);

        Assert.Equal("Valve", breakdown.TopDevelopers[0].Name);
        Assert.Equal(150.0, breakdown.TopDevelopers[0].PlaytimeHours);
        Assert.Equal(2, breakdown.TopDevelopers[0].GameCount);

        Assert.Equal("FromSoftware", breakdown.TopDevelopers[1].Name);
        Assert.Equal(50.0, breakdown.TopDevelopers[1].PlaytimeHours);
        Assert.Equal(2, breakdown.TopDevelopers[1].GameCount);

        Assert.Equal("ConcernedApe", breakdown.TopDevelopers[2].Name);
        Assert.Equal(40.0, breakdown.TopDevelopers[2].PlaytimeHours);
        Assert.Equal(1, breakdown.TopDevelopers[2].GameCount);
    }

    [Fact]
    public void Build_OrdersTopDevelopersAndTagsByCountSeparatelyFromPlaytime()
    {
        var owned = new List<(int AppId, double PlaytimeHours)>
        {
            (1, 500),
            (2, 10),
            (3, 10),
            (4, 10)
        };

        var catalog = new Dictionary<int, SteamCatalogGame>
        {
            [1] = Meta(1, ["Action"], ["FPS"], ["Studio A"]),
            [2] = Meta(2, ["Indie"], ["Roguelike"], ["Studio B"]),
            [3] = Meta(3, ["Indie"], ["Roguelike"], ["Studio B"]),
            [4] = Meta(4, ["Indie"], ["Roguelike"], ["Studio B"])
        };

        var breakdown = LibraryBreakdownBuilder.Build(owned, catalog);

        Assert.NotNull(breakdown);
        Assert.Equal("Studio A", breakdown.TopDevelopers[0].Name);
        Assert.Equal("FPS", breakdown.TopTags[0].Name);

        Assert.NotNull(breakdown.TopDevelopersByCount);
        Assert.Equal("Studio B", breakdown.TopDevelopersByCount[0].Name);
        Assert.Equal(3, breakdown.TopDevelopersByCount[0].GameCount);

        Assert.NotNull(breakdown.TopTagsByCount);
        Assert.Equal("Roguelike", breakdown.TopTagsByCount[0].Name);
        Assert.Equal(3, breakdown.TopTagsByCount[0].GameCount);
    }

    private static SteamCatalogGame Meta(int appId, string[] genres, string[] tags, string[] devs) =>
        new(appId, $"Game {appId}", genres, tags, devs, devs, null, null, null, null, null, null, null);
}
