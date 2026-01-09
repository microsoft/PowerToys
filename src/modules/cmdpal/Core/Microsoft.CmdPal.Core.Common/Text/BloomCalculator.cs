// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace Microsoft.CmdPal.Core.Common.Text;

public sealed class BloomCalculator : IBloomCalculator
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ComputeBloomFilter(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return 0;
        }

        ulong bloom = 0;
        foreach (var ch in input)
        {
            // 0. Ignore separators
            if (ch is '/' or '\\' or '_' or '-' or '.' or ' ' or '\'' or '"' or ':')
            {
                continue;
            }

            // 1. Better, faster mixer than the 64-bit version for a 32-bit input
            // Using a prime multiplier for a quick, cheap distribution
            var h = (uint)ch * 0x45d9f3b;

            // 2. Map into two distinct 32-bit halves of the 64-bit filter.
            // This ensures a single character doesn't "clump" its bits.
            // Bit 0-31 for the first hash, Bit 32-63 for the second.
            var h1 = h & 31;
            var h2 = (h >> 16) & 31;

            bloom |= 1UL << (int)h1;
            bloom |= 1UL << (int)(h2 + 32);

            // Early exit if saturated
            if (bloom == ulong.MaxValue)
            {
                break;
            }
        }

        return bloom;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MightContain(ulong candidateBloom, ulong queryBloom)
        => (candidateBloom & queryBloom) == queryBloom;
}
