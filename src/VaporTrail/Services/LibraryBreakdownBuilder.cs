using System.Text.RegularExpressions;
using VaporTrail.Models;

namespace VaporTrail.Services;

public static partial class LibraryBreakdownBuilder
{
    private const int MaxGenreSlices = 5;
    private const int MaxTagSlices = 10;
    private const int MaxDeveloperSlices = 6;

    [GeneratedRegex(@"(?:,?\s+(?:Corporation|Corp|Incorporated|Inc|Limited|Ltd|LLC|GmbH|Co|AB|B\.V|S\.A|K\.K|S\.r\.l|Pty)\.?)+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DeveloperSuffixRegex();

    public static LibraryBreakdown? Build(
        IReadOnlyList<(int AppId, double PlaytimeHours)> ownedGames,
        IReadOnlyDictionary<int, SteamCatalogGame> catalog)
    {
        if (ownedGames.Count == 0 || catalog.Count == 0)
        {
            return null;
        }

        var genreBuckets = new Dictionary<string, (double Hours, int Count)>(StringComparer.OrdinalIgnoreCase);
        var tagBuckets = new Dictionary<string, (double Hours, int Count)>(StringComparer.OrdinalIgnoreCase);
        var devBuckets = new Dictionary<string, (double Hours, int Count)>(StringComparer.OrdinalIgnoreCase);

        var matchedCount = 0;
        var matchedPlaytimeTotal = 0.0;

        foreach (var (appId, hours) in ownedGames)
        {
            if (!catalog.TryGetValue(appId, out var meta))
            {
                continue;
            }

            matchedCount++;
            var safeHours = Math.Max(0, hours);
            matchedPlaytimeTotal += safeHours;

            Accumulate(genreBuckets, meta.Genres, safeHours);
            Accumulate(tagBuckets, meta.Tags, safeHours);

            var primaryDeveloper = meta.Developers.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));
            if (primaryDeveloper is not null)
            {
                var normalizedDev = NormalizeDeveloperName(primaryDeveloper);
                devBuckets.TryGetValue(normalizedDev, out var current);
                devBuckets[normalizedDev] = (current.Hours + safeHours, current.Count + 1);
            }
        }

        if (matchedCount == 0)
        {
            return null;
        }

        var genresByPlaytime = BuildRollupSlices(genreBuckets, byPlaytime: true, MaxGenreSlices);
        var genresByCount = BuildRollupSlices(genreBuckets, byPlaytime: false, MaxGenreSlices);

        var topTags = BuildTopSlices(
            tagBuckets,
            MaxTagSlices,
            matchedPlaytimeTotal,
            matchedCount);

        var devTotalHours = devBuckets.Values.Sum(v => v.Hours);
        var devTotalCount = devBuckets.Values.Sum(v => v.Count);
        var topDevelopers = BuildTopSlices(
            devBuckets,
            MaxDeveloperSlices,
            devTotalHours,
            devTotalCount);

        return new LibraryBreakdown(
            AnalyzedGamesCount: ownedGames.Count,
            MatchedGamesCount: matchedCount,
            GenresByPlaytime: genresByPlaytime,
            GenresByCount: genresByCount,
            TopTags: topTags,
            TopDevelopers: topDevelopers
        );
    }

    private static void Accumulate(
        Dictionary<string, (double Hours, int Count)> buckets,
        IReadOnlyList<string> labels,
        double hours)
    {
        if (labels.Count == 0)
        {
            return;
        }

        foreach (var label in labels.Where(l => !string.IsNullOrWhiteSpace(l)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var trimmed = label.Trim();
            buckets.TryGetValue(trimmed, out var current);
            buckets[trimmed] = (current.Hours + hours, current.Count + 1);
        }
    }

    private static List<BreakdownSlice> BuildRollupSlices(
        Dictionary<string, (double Hours, int Count)> buckets,
        bool byPlaytime,
        int maxSlices)
    {
        if (buckets.Count == 0)
        {
            return [];
        }

        var ordered = byPlaytime
            ? buckets
                .Where(kv => kv.Value.Hours > 0)
                .OrderByDescending(kv => kv.Value.Hours)
                .ThenByDescending(kv => kv.Value.Count)
                .ToList()
            : buckets
                .OrderByDescending(kv => kv.Value.Count)
                .ThenByDescending(kv => kv.Value.Hours)
                .ToList();

        if (ordered.Count == 0)
        {
            ordered = buckets
                .OrderByDescending(kv => kv.Value.Count)
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            byPlaytime = false;
        }

        var totalWeight = byPlaytime
            ? ordered.Sum(kv => kv.Value.Hours)
            : ordered.Sum(kv => (double)kv.Value.Count);

        if (totalWeight <= 0)
        {
            return [];
        }

        var top = ordered.Take(maxSlices).ToList();
        var rest = ordered.Skip(maxSlices).ToList();

        var result = new List<BreakdownSlice>(top.Count + (rest.Count > 0 ? 1 : 0));
        foreach (var item in top)
        {
            var weight = byPlaytime ? item.Value.Hours : item.Value.Count;
            result.Add(new BreakdownSlice(
                Name: item.Key,
                PlaytimeHours: Math.Round(item.Value.Hours, 1),
                GameCount: item.Value.Count,
                SharePercent: Math.Round(weight / totalWeight * 100.0, 1)
            ));
        }

        if (rest.Count > 0)
        {
            var otherHours = rest.Sum(kv => kv.Value.Hours);
            var otherCount = rest.Sum(kv => kv.Value.Count);
            var otherWeight = byPlaytime ? otherHours : otherCount;
            result.Add(new BreakdownSlice(
                Name: "Other",
                PlaytimeHours: Math.Round(otherHours, 1),
                GameCount: otherCount,
                SharePercent: Math.Round(otherWeight / totalWeight * 100.0, 1)
            ));
        }

        return result;
    }

    private static List<BreakdownSlice> BuildTopSlices(
        Dictionary<string, (double Hours, int Count)> buckets,
        int limit,
        double denominatorHours,
        int denominatorCount)
    {
        if (buckets.Count == 0)
        {
            return [];
        }

        var useHours = denominatorHours > 0;
        var denominator = useHours ? denominatorHours : denominatorCount;

        return buckets
            .OrderByDescending(kv => kv.Value.Hours)
            .ThenByDescending(kv => kv.Value.Count)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(kv =>
            {
                var weight = useHours ? kv.Value.Hours : kv.Value.Count;
                var share = denominator > 0 ? Math.Round(weight / denominator * 100.0, 1) : 0;
                return new BreakdownSlice(
                    Name: kv.Key,
                    PlaytimeHours: Math.Round(kv.Value.Hours, 1),
                    GameCount: kv.Value.Count,
                    SharePercent: share
                );
            })
            .ToList();
    }

    public static string NormalizeDeveloperName(string raw)
    {
        var trimmed = raw.Trim();
        var stripped = DeveloperSuffixRegex().Replace(trimmed, string.Empty).Trim().TrimEnd(',', ' ');
        return string.IsNullOrWhiteSpace(stripped) ? trimmed : stripped;
    }
}
