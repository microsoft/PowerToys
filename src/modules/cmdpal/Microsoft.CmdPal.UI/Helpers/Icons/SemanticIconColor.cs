// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Resolves the shared semantic color vocabulary used by generated and themed icons.
/// </summary>
internal static class SemanticIconColor
{
    public static string GetDefault(ElementTheme theme) =>
        theme == ElementTheme.Dark ? "#60CDFF" : "#0067C0";

    public static bool IsSemantic(ReadOnlySpan<char> value) =>
        TryResolvePair(value, out _, out _);

    public static bool TryResolve(
        ReadOnlySpan<char> value,
        ElementTheme theme,
        out string color)
    {
        if (!TryResolvePair(value, out var light, out var dark))
        {
            color = string.Empty;
            return false;
        }

        color = theme == ElementTheme.Dark ? dark : light;
        return true;
    }

    public static bool TryResolvePair(
        ReadOnlySpan<char> value,
        out string light,
        out string dark)
    {
        if (value.Equals("danger", StringComparison.OrdinalIgnoreCase))
        {
            light = "#C42B1C";
            dark = "#FF99A4";
            return true;
        }

        if (value.Equals("subtle", StringComparison.OrdinalIgnoreCase))
        {
            light = "#616161";
            dark = "#C5C5C5";
            return true;
        }

        if (value.Equals("info", StringComparison.OrdinalIgnoreCase))
        {
            light = "#0067C0";
            dark = "#60CDFF";
            return true;
        }

        if (value.Equals("warning", StringComparison.OrdinalIgnoreCase))
        {
            light = "#9D5D00";
            dark = "#FCE100";
            return true;
        }

        if (value.Equals("success", StringComparison.OrdinalIgnoreCase))
        {
            light = "#0F7B0F";
            dark = "#6CCB5F";
            return true;
        }

        if (value.Equals("neutral", StringComparison.OrdinalIgnoreCase))
        {
            light = "#8A8A8A";
            dark = "#9D9D9D";
            return true;
        }

        if (value.Equals("dark", StringComparison.OrdinalIgnoreCase))
        {
            light = "#1B1A19";
            dark = "#1B1A19";
            return true;
        }

        if (value.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            light = "#000000";
            dark = "#FFFFFF";
            return true;
        }

        if (value.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            light = "#00000000";
            dark = "#00000000";
            return true;
        }

        light = string.Empty;
        dark = string.Empty;
        return false;
    }
}
