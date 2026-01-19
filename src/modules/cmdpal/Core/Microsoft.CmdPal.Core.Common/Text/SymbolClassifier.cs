// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace Microsoft.CmdPal.Core.Common.Text;

internal static class SymbolClassifier
{
    // Embedded in .data section - no allocation, no static constructor
    private static ReadOnlySpan<byte> Lookup =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 0-15
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 16-31
        2, 0, 2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 2, 2, 1, // 32-47: space=2, "=2, '=2, -=2, .=2, /=1
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, // 48-63: :=2
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 64-79
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 2, // 80-95: _=2
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 96-111
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 // 112-127
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SymbolKind Classify(char c)
    {
        return c > 0x7F ? SymbolKind.Other : (SymbolKind)Lookup[c];
    }
}
