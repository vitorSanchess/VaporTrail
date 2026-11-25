using VaporTrail.Models;

namespace VaporTrail.Services;

public static class BacklogEngagementBuilder
{
    private const int MaxBacklogPicks = 4;

    public static BacklogEngagement? Build(
        IReadOnlyList<(int AppId, string Name, double PlaytimeHours)> ownedGames,
        IReadOnlyDictionary<int, SteamCatalogGame> catalog)
    {
        if (ownedGames.Count == 0)
        {
            return null;
        }

        var unplayedCount = 0;
        var sampledCount = 0;
        var playedCount = 0;
        var dedicatedCount = 0;

        var playedHoursList = new List<double>();
        var totalPlaytime = 0.0;

        var backlogMatches = new List<(int AppId, string Name, double HltbMainHours)>();

        foreach (var (appId, name, rawHours) in ownedGames)
        {
            var hours = Math.Max(0, rawHours);
            totalPlaytime += hours;

            if (hours <= 0)
            {
                unplayedCount++;
                if (catalog.TryGetValue(appId, out var meta) && meta.HltbMainHours is > 0)
                {
                    var displayName = string.IsNullOrWhiteSpace(name) ? meta.Name : name;
                    backlogMatches.Add((appId, displayName, meta.HltbMainHours.Value));
                }
            }
            else
            {
                playedHoursList.Add(hours);
                if (hours < 2.0)
                {
                    sampledCount++;
                }
                else if (hours < 20.0)
                {
                    playedCount++;
                }
                else
                {
                    dedicatedCount++;
                }
            }
        }

        var totalGames = ownedGames.Count;
        var totalPlayedGames = playedHoursList.Count;
        var unplayedPercent = Math.Round((double)unplayedCount / totalGames * 100.0, 1);

        var top3Hours = ownedGames
            .Select(g => Math.Max(0, g.PlaytimeHours))
            .OrderByDescending(h => h)
            .Take(3)
            .Sum();

        var top3Share = totalPlaytime > 0
            ? Math.Round(top3Hours / totalPlaytime * 100.0, 1)
            : 0.0;

        double? medianPlayed = null;
        if (totalPlayedGames > 0)
        {
            playedHoursList.Sort();
            var mid = totalPlayedGames / 2;
            var rawMedian = totalPlayedGames % 2 == 1
                ? playedHoursList[mid]
                : (playedHoursList[mid - 1] + playedHoursList[mid]) / 2.0;
            medianPlayed = Math.Round(rawMedian, 1);
        }

        double? estimatedBacklogHours = backlogMatches.Count > 0
            ? Math.Round(backlogMatches.Sum(x => x.HltbMainHours), 1)
            : null;

        var quickestToBeat = backlogMatches
            .OrderBy(x => x.HltbMainHours)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxBacklogPicks)
            .Select(x => new BacklogCandidate(
                AppId: x.AppId,
                Name: x.Name,
                HltbMainHours: Math.Round(x.HltbMainHours, 1),
                StoreUrl: $"https://store.steampowered.com/app/{x.AppId}"
            ))
            .ToList();

        var depthBuckets = new List<EngagementBucket>(4)
        {
            Bucket("unplayed", "Unplayed", unplayedCount, totalGames),
            Bucket("sampled", "Under 2h", sampledCount, totalGames),
            Bucket("played", "2\u201320h", playedCount, totalGames),
            Bucket("dedicated", "20h+", dedicatedCount, totalGames)
        };

        return new BacklogEngagement(
            TotalGamesCount: totalGames,
            PlayedGamesCount: totalPlayedGames,
            UnplayedGamesCount: unplayedCount,
            UnplayedPercent: unplayedPercent,
            Top3PlaytimeHours: Math.Round(top3Hours, 1),
            Top3PlaytimeSharePercent: top3Share,
            MedianPlayedHours: medianPlayed,
            EstimatedBacklogHours: estimatedBacklogHours,
            BacklogMatchedGamesCount: backlogMatches.Count,
            DepthBuckets: depthBuckets,
            QuickestToBeat: quickestToBeat
        );
    }

    private static EngagementBucket Bucket(string key, string label, int count, int total) =>
        new(
            Key: key,
            Label: label,
            GameCount: count,
            SharePercent: total > 0 ? Math.Round((double)count / total * 100.0, 1) : 0.0
        );
}
