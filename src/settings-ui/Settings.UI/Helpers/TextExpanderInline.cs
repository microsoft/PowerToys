// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>One piece of a line: how it's drawn, its text, and the part of the snippet it shows.</summary>
    internal readonly record struct TextExpanderInline(TextExpanderInlineKind Kind, string Text, TextExpanderPreviewSegment Segment)
    {
        /// <summary>Gets a value indicating whether this piece is an element in the line rather than text.</summary>
        public bool IsElement => Kind is TextExpanderInlineKind.Bubble or TextExpanderInlineKind.CursorBar or TextExpanderInlineKind.BreakMarker;
    }
}
