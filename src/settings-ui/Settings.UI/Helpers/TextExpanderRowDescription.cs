// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>What a snippet row shows besides its trigger.</summary>
    /// <param name="Sentence">A plain sentence for a dynamic snippet, as plain text, or null to show the saved text instead.</param>
    /// <param name="SentenceParts">The sentence with its variables as bubbles; empty when there is no sentence.</param>
    /// <param name="Segments">The saved text, with the editor's bubbles for values filled in when it runs.</param>
    /// <param name="TitleSegments">Without a sentence, the saved text as the title: up to two of its lines that aren't blank, then "…" if more follow.</param>
    /// <param name="ShowsSegmentsBelow">Whether the saved text goes under the sentence; only when it has words of its own.</param>
    /// <param name="Warning">Why the snippet never expands, or null.</param>
    /// <param name="AutomationName">What a screen reader says for the row, including how many lines it has.</param>
    public sealed record TextExpanderRowDescription(
        string Sentence,
        IReadOnlyList<TextExpanderPreviewSegment> SentenceParts,
        IReadOnlyList<TextExpanderPreviewSegment> Segments,
        IReadOnlyList<TextExpanderPreviewSegment> TitleSegments,
        bool ShowsSegmentsBelow,
        string Warning,
        string AutomationName);
}
