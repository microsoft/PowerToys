// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Composes the screen reader announcement raised after the visible results change.
/// </summary>
internal static class SearchResultsAnnouncement
{
    public static string Compose(
        int resultCount,
        string? selectedTitle,
        string noResultsText,
        string oneResultText,
        CompositeFormat resultsCountFormat,
        CompositeFormat resultsWithSelectedItemFormat,
        IFormatProvider? provider)
    {
        if (resultCount <= 0)
        {
            return noResultsText;
        }

        var countText = resultCount == 1
            ? oneResultText
            : string.Format(provider, resultsCountFormat, resultCount);

        return string.IsNullOrWhiteSpace(selectedTitle)
            ? countText
            : string.Format(provider, resultsWithSelectedItemFormat, countText, selectedTitle);
    }
}
