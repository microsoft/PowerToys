// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// One piece of the snippet editor's preview: plain text, a line break or tab, or a value that
    /// is only known when the snippet runs.
    /// </summary>
    /// <param name="Kind">What the piece stands for.</param>
    /// <param name="Value">The text, the date or time shown, or a question's label.</param>
    public readonly record struct TextExpanderPreviewSegment(TextExpanderPreviewSegmentKind Kind, string Value = "")
    {
        /// <summary>Gets a value indicating whether the preview shows this as a bubble.</summary>
        public bool IsBubble => Kind is TextExpanderPreviewSegmentKind.Date
            or TextExpanderPreviewSegmentKind.Time
            or TextExpanderPreviewSegmentKind.Clipboard
            or TextExpanderPreviewSegmentKind.Prompt;
    }
}
