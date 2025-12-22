// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Globalization;
using System.Text;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public static class NormalizationHelper
{
    private const int StackAllocThreshold = 256;

    public static string NormalizeString(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var formD = input.Normalize(NormalizationForm.FormD);
        var len = formD.Length;

        char[]? rented = null;
        var spanBuffer = len <= StackAllocThreshold
            ? stackalloc char[len]
            : rented = ArrayPool<char>.Shared.Rent(len);

        var written = 0;

        try
        {
            foreach (var ch in formD)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
                {
                    continue;
                }

                spanBuffer[written++] = ch;
            }

            var result = new string(spanBuffer[..written]);
            return result.ToUpperInvariant();
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
}
