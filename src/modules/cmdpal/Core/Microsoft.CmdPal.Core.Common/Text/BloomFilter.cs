// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace Microsoft.CmdPal.Core.Common.Text;

public sealed class BloomFilter : IBloomFilter
{
    public ulong Compute(string input)
    {
        ulong bloom = 0;

        foreach (var ch in input)
        {
            if (SymbolClassifier.Classify(ch) == SymbolKind.WordSeparator)
            {
                continue;
            }

            var h = (uint)ch * 0x45d9f3b;
            bloom |= 1UL << (int)(h & 31);
            bloom |= 1UL << (int)(((h >> 16) & 31) + 32);

            if (bloom == ulong.MaxValue)
            {
                break;
            }
        }

        return bloom;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MightContain(ulong candidateBloom, ulong queryBloom)
    {
        return (candidateBloom & queryBloom) == queryBloom;
    }
}
