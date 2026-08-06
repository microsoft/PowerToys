// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Diagnostics;
using Microsoft.CmdPal.Common.Text;

namespace Microsoft.CmdPal.Common.Helpers;

public static partial class InternalListHelpers
{
    public static RoScored<T>[] FilterListWithScores<T>(
        IEnumerable<T>? items,
        in FuzzyQuery query,
        in ScoringFunction<T> scoreFunction)
    {
        if (items == null)
        {
            return [];
        }

        // Try to get initial capacity hint
        var initialCapacity = items switch
        {
            ICollection<T> col => col.Count,
            IReadOnlyCollection<T> rc => rc.Count,
            _ => 64,
        };

        var buffer = ArrayPool<RoScored<T>>.Shared.Rent(initialCapacity);
        var count = 0;

        try
        {
            foreach (var item in items)
            {
                var score = scoreFunction(in query, item);
                if (score <= 0)
                {
                    continue;
                }

                if (count == buffer.Length)
                {
                    GrowBuffer(ref buffer, count);
                }

                buffer[count++] = new RoScored<T>(item, score);
            }

            Array.Sort(buffer, 0, count, default(RoScoredDescendingComparer<T>));
            var result = GC.AllocateUninitializedArray<RoScored<T>>(count);
            buffer.AsSpan(0, count).CopyTo(result);
            return result;
        }
        finally
        {
            ArrayPool<RoScored<T>>.Shared.Return(buffer);
        }
    }

    // Minimum item count before the parallel path earns back its partitioning and merge overhead,
    // which keeps commands and small app sets on the serial path.
    private const int ParallelScoringThreshold = 512;

    /// <summary>
    /// Order-preserving parallel variant of <see cref="FilterListWithScores{T}"/> that scores
    /// contiguous index ranges on separate threads and concatenates them back in partition order,
    /// producing a pre-sort buffer identical to the serial path. The scoring function has to be
    /// pure per item, since each item is scored by exactly one thread.
    /// </summary>
    public static RoScored<T>[] FilterListWithScoresParallel<T>(
        IReadOnlyList<T>? items,
        in FuzzyQuery query,
        in ScoringFunction<T> scoreFunction)
    {
        if (items is null || items.Count == 0)
        {
            return [];
        }

        var count = items.Count;
        var partitions = Math.Min(Environment.ProcessorCount, Math.Max(1, count / ParallelScoringThreshold));

        if (count < ParallelScoringThreshold || partitions <= 1)
        {
            return FilterListWithScores(items, query, scoreFunction);
        }

        // Copy the by-ref parameters into locals so the parallel body can capture them. FuzzyQuery
        // is immutable, so one shared copy is safe to read from every thread.
        var q = query;
        var fn = scoreFunction;
        var source = items;

        var partitionResults = new List<RoScored<T>>[partitions];

        System.Threading.Tasks.Parallel.For(0, partitions, p =>
        {
            var start = (int)((long)p * count / partitions);
            var end = (int)((long)(p + 1) * count / partitions);

            var local = new List<RoScored<T>>(end - start);
            for (var i = start; i < end; i++)
            {
                var item = source[i];
                var score = fn(in q, item);
                if (score > 0)
                {
                    local.Add(new RoScored<T>(item, score));
                }
            }

            partitionResults[p] = local;
        });

        var total = 0;
        for (var p = 0; p < partitions; p++)
        {
            total += partitionResults[p].Count;
        }

        // Contiguous ranges merged in partition order reproduce the serial buffer's exact
        // enumeration order.
        var buffer = GC.AllocateUninitializedArray<RoScored<T>>(total);
        var pos = 0;
        for (var p = 0; p < partitions; p++)
        {
            var list = partitionResults[p];
            for (var j = 0; j < list.Count; j++)
            {
                buffer[pos++] = list[j];
            }
        }

        Array.Sort(buffer, 0, total, default(RoScoredDescendingComparer<T>));
        return buffer;
    }

    private static void GrowBuffer<T>(ref RoScored<T>[] buffer, int count)
    {
        var newBuffer = ArrayPool<RoScored<T>>.Shared.Rent(buffer.Length * 2);
        buffer.AsSpan(0, count).CopyTo(newBuffer);
        ArrayPool<RoScored<T>>.Shared.Return(buffer);
        buffer = newBuffer;
    }

    public static T[] FilterList<T>(IEnumerable<T> items, in FuzzyQuery query, ScoringFunction<T> scoreFunction)
    {
        // Try to get initial capacity hint
        var initialCapacity = items switch
        {
            ICollection<T> col => col.Count,
            IReadOnlyCollection<T> rc => rc.Count,
            _ => 64,
        };

        var buffer = ArrayPool<RoScored<T>>.Shared.Rent(initialCapacity);
        var count = 0;

        try
        {
            foreach (var item in items)
            {
                var score = scoreFunction(in query, item);
                if (score <= 0)
                {
                    continue;
                }

                if (count == buffer.Length)
                {
                    GrowBuffer(ref buffer, count);
                }

                buffer[count++] = new RoScored<T>(item, score);
            }

            Array.Sort(buffer, 0, count, default(RoScoredDescendingComparer<T>));

            var result = GC.AllocateUninitializedArray<T>(count);
            for (var i = 0; i < count; i++)
            {
                result[i] = buffer[i].Item;
            }

            return result;
        }
        finally
        {
            ArrayPool<RoScored<T>>.Shared.Return(buffer);
        }
    }

    private readonly struct RoScoredDescendingComparer<T> : IComparer<RoScored<T>>
    {
        public int Compare(RoScored<T> x, RoScored<T> y) => y.Score.CompareTo(x.Score);
    }
}

public delegate int ScoringFunction<in T>(in FuzzyQuery query, T item);

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct RoScored<T>
{
    public readonly int Score;
    public readonly T Item;

    public RoScored(T item, int score)
    {
        Score = score;
        Item = item;
    }

    private string GetDebuggerDisplay()
    {
        return "Score = " + Score + ", Item = " + Item;
    }
}
