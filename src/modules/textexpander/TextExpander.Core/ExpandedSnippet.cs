// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.PowerToys.TextExpander;

/// <summary>A snippet expanded: its text, where its cursor marker was, and the spots its clipboard variables left empty.</summary>
/// <param name="Text">The text, without the snippet's first cursor marker.</param>
/// <param name="Cursor">Where that marker was in <paramref name="Text"/>, or -1.</param>
/// <param name="Gaps">The spots clipboard variables left empty, in the snippet's order, and why; none for most snippets.</param>
public readonly record struct ExpandedSnippet(string Text, int Cursor, IReadOnlyList<ClipboardGap> Gaps)
{
    /// <summary>Gets the text and where the cursor marker was, for callers that don't tell the user about empty spots.</summary>
    public void Deconstruct(out string text, out int cursor)
    {
        text = Text;
        cursor = Cursor;
    }
}
