// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Graphics.Canvas.Text;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class SystemFontHelper
{
    private static List<string>? _cachedFontFamilies;

    public static List<string> GetSystemFontFamilies()
    {
        if (_cachedFontFamilies is not null)
        {
            return _cachedFontFamilies;
        }

        var fontFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var fontSet = CanvasFontSet.GetSystemFontSet();
        foreach (var face in fontSet.Fonts)
        {
            var familyNames = face.FamilyNames;
            if (familyNames.Count > 0)
            {
                // Prefer the en-us name; fall back to the first available name
                if (!familyNames.TryGetValue("en-us", out var name))
                {
                    name = familyNames.Values.FirstOrDefault();
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    fontFamilies.Add(name);
                }
            }
        }

        _cachedFontFamilies = [.. fontFamilies.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)];
        return _cachedFontFamilies;
    }
}
