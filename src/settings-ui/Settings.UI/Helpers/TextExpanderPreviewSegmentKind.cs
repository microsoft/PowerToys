// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>What one piece of the snippet editor's preview stands for.</summary>
    public enum TextExpanderPreviewSegmentKind
    {
        /// <summary>Text that is typed exactly as shown.</summary>
        Text,

        /// <summary>A new line.</summary>
        LineBreak,

        /// <summary>A tab.</summary>
        Tab,

        /// <summary>A date filled in when the snippet runs. The value is today's.</summary>
        Date,

        /// <summary>The time when the snippet runs. The value is the current time.</summary>
        Time,

        /// <summary>Whatever is on the clipboard when the snippet runs.</summary>
        Clipboard,

        /// <summary>An answer asked for when the snippet runs. The value is the question's label.</summary>
        Prompt,

        /// <summary>Where the cursor is left afterwards.</summary>
        Cursor,
    }
}
