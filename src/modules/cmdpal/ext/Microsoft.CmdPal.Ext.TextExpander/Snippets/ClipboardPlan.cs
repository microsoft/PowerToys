// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>What one paste of a snippet takes from the clipboard, worked out by <see cref="SnippetText.PlanClipboard(string, IReadOnlyList{string}, string)"/>.</summary>
/// <param name="Copy">The clipboard's text, read once, or null when it held none.</param>
/// <param name="Choices">One choice for each spot whose part has several matches, in the order Text Expander asks them.</param>
/// <param name="Gaps">The spots Core leaves empty because <paramref name="Copy"/> has nothing for them, in the snippet's order.</param>
internal sealed record ClipboardPlan(string? Copy, IReadOnlyList<ClipboardChoice> Choices, IReadOnlyList<ClipboardGap> Gaps)
{
    private readonly List<(string? Text, ClipboardPart Part, CultureInfo Culture, IReadOnlyList<string> Found)> _found = [];

    /// <summary>Gets what finds a part in the copy when it isn't kept yet. Tests pass their own.</summary>
    internal Func<string?, ClipboardPart, CultureInfo, IReadOnlyList<string>> Finder { get; init; } = ClipboardParts.Find;

    /// <summary>
    /// Finds a part in the copy as <see cref="ClipboardParts.Find"/> does, once for each part: a pick page expands the
    /// snippet again for the preview of every match, and a copy can be long. Kept no longer than the plan keeps the copy.
    /// </summary>
    internal IReadOnlyList<string> FindParts(string? text, ClipboardPart part, CultureInfo culture)
    {
        lock (_found)
        {
            foreach (var entry in _found)
            {
                if (entry.Part == part && entry.Culture.Equals(culture) && (ReferenceEquals(entry.Text, text) || string.Equals(entry.Text, text, StringComparison.Ordinal)))
                {
                    return entry.Found;
                }
            }
        }

        var found = Finder(text, part, culture);
        lock (_found)
        {
            _found.Add((text, part, culture, found));
        }

        return found;
    }
}
