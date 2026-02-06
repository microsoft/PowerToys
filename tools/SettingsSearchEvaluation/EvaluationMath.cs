// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace SettingsSearchEvaluation;

internal static class EvaluationMath
{
    public static int FindBestRank(IReadOnlyList<string> rankedResultIds, IReadOnlySet<string> expectedIds)
    {
        ArgumentNullException.ThrowIfNull(rankedResultIds);
        ArgumentNullException.ThrowIfNull(expectedIds);

        if (expectedIds.Count == 0 || rankedResultIds.Count == 0)
        {
            return 0;
        }

        for (int index = 0; index < rankedResultIds.Count; index++)
        {
            if (expectedIds.Contains(rankedResultIds[index]))
            {
                return index + 1;
            }
        }

        return 0;
    }

    public static LatencySummary ComputeLatencySummary(IReadOnlyList<double> samplesMs)
    {
        ArgumentNullException.ThrowIfNull(samplesMs);

        if (samplesMs.Count == 0)
        {
            return LatencySummary.Empty;
        }

        var sorted = samplesMs.OrderBy(x => x).ToArray();
        var total = samplesMs.Sum();

        return new LatencySummary
        {
            Samples = sorted.Length,
            MinMs = sorted[0],
            P50Ms = Percentile(sorted, 0.50),
            P95Ms = Percentile(sorted, 0.95),
            MaxMs = sorted[^1],
            AverageMs = total / sorted.Length,
        };
    }

    private static double Percentile(IReadOnlyList<double> sortedSamples, double percentile)
    {
        if (sortedSamples.Count == 0)
        {
            return 0;
        }

        var clamped = Math.Clamp(percentile, 0, 1);
        var rank = (int)Math.Ceiling(clamped * sortedSamples.Count) - 1;
        rank = Math.Clamp(rank, 0, sortedSamples.Count - 1);
        return sortedSamples[rank];
    }
}
