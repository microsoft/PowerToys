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
