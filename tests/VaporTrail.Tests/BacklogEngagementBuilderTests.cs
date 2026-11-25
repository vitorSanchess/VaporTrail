using VaporTrail.Models;
using VaporTrail.Services;

namespace VaporTrail.Tests;

public class BacklogEngagementBuilderTests
{
    [Fact]
    public void Build_ComputesDepthBucketsConcentrationMedianAndBacklogEstimate()
    {
        var owned = new List<(int AppId, string Name, double PlaytimeHours)>
        {
            (1, "Giant RPG", 100.0),
            (2, "Co-op Shooter", 60.0),
            (3, "Strategy Hit", 40.0),
            (4, "Mid Indie", 10.0),
            (5, "Short Try", 1.0),
            (6, "Backlog Long", 0.0),
            (7, "Backlog Short", 0.0),
            (8, "Backlog Medium", 0.0),
            (9, "Backlog Tiny", 0.0),
            (10, "Backlog Extra", 0.0),
            (11, "Backlog Unknown", 0.0)
        };

        var catalog = new Dictionary<int, SteamCatalogGame>
        {
            [6] = WithHltb(6, "Backlog Long", 55.0),
            [7] = WithHltb(7, "Backlog Short", 5.5),
            [8] = WithHltb(8, "Backlog Medium", 14.0),
            [9] = WithHltb(9, "Backlog Tiny", 2.5),
            [10] = WithHltb(10, "Backlog Extra", 21.0),
            [11] = WithHltb(11, "Backlog Unknown", null)
        };

        var result = BacklogEngagementBuilder.Build(owned, catalog);

        Assert.NotNull(result);
        Assert.Equal(11, result.TotalGamesCount);
        Assert.Equal(5, result.PlayedGamesCount);
        Assert.Equal(6, result.UnplayedGamesCount);
        Assert.Equal(54.5, result.UnplayedPercent);

        // Top 3 = 100 + 60 + 40 = 200h out of 211h total
        Assert.Equal(200.0, result.Top3PlaytimeHours);
        Assert.Equal(94.8, result.Top3PlaytimeSharePercent);

        // Played hours sorted: [1, 10, 40, 60, 100] -> median 40.0
        Assert.Equal(40.0, result.MedianPlayedHours);

        // HLTB sum: 55 + 5.5 + 14 + 2.5 + 21 = 98.0 across 5 matched unplayed games
        Assert.Equal(98.0, result.EstimatedBacklogHours);
        Assert.Equal(5, result.BacklogMatchedGamesCount);

        // Quickest 4: 2.5, 5.5, 14.0, 21.0
        Assert.Equal(4, result.QuickestToBeat.Count);
        Assert.Equal(["Backlog Tiny", "Backlog Short", "Backlog Medium", "Backlog Extra"],
            result.QuickestToBeat.Select(x => x.Name).ToArray());

        // Depth buckets: Unplayed=6, Under 2h=1, 2–20h=1, 20h+=3
        Assert.Equal(4, result.DepthBuckets.Count);
        Assert.Equal(6, result.DepthBuckets.Single(b => b.Key == "unplayed").GameCount);
        Assert.Equal(1, result.DepthBuckets.Single(b => b.Key == "sampled").GameCount);
        Assert.Equal(1, result.DepthBuckets.Single(b => b.Key == "played").GameCount);
        Assert.Equal(3, result.DepthBuckets.Single(b => b.Key == "dedicated").GameCount);
    }

    [Fact]
    public void Build_AllGamesPlayed_ReturnsZeroUnplayedAndNullBacklogEstimate()
    {
        var owned = new List<(int AppId, string Name, double PlaytimeHours)>
        {
            (1, "Game One", 12.0),
            (2, "Game Two", 24.0)
        };

        var result = BacklogEngagementBuilder.Build(owned, new Dictionary<int, SteamCatalogGame>());

        Assert.NotNull(result);
        Assert.Equal(0, result.UnplayedGamesCount);
        Assert.Equal(0.0, result.UnplayedPercent);
        Assert.Equal(18.0, result.MedianPlayedHours);
        Assert.Null(result.EstimatedBacklogHours);
        Assert.Equal(0, result.BacklogMatchedGamesCount);
        Assert.Empty(result.QuickestToBeat);
    }

    private static SteamCatalogGame WithHltb(int appId, string name, double? hltbMainHours) =>
        new(appId, name, [], [], [], [], null, null, null, null, null, hltbMainHours, null);
}
