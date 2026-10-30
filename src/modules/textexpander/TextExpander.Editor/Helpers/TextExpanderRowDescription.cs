// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>What a snippet's row shows besides its trigger, in Keyboard Manager's style.</summary>
    /// <param name="KindGlyph">The accent chip's icon: text, a date, a time or the clipboard, or the one value the snippet is.</param>
    /// <param name="Lead">The words before the chip: "inserts", or "asks", each question once as a bubble, then "then inserts".</param>
    /// <param name="Chip">What it inserts, as its real lines with bubbles: at most three, then "…" if more follow.</param>
    /// <param name="Warning">Why the snippet never expands, or null.</param>
    /// <param name="AutomationName">What a screen reader says for the row: the trigger, the text and any warning.</param>
    public sealed record TextExpanderRowDescription(
        string KindGlyph,
        IReadOnlyList<TextExpanderPreviewSegment> Lead,
        IReadOnlyList<TextExpanderPreviewSegment> Chip,
        string Warning,
        string AutomationName);
}
