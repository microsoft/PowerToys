// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>What draws one piece of a snippet's text in <c>TextExpanderBubbleText</c>.</summary>
    internal enum TextExpanderInlineKind
    {
        /// <summary>Text in the line's own font.</summary>
        Text,

        /// <summary>A new line.</summary>
        LineBreak,

        /// <summary>A value's bubble, an element in the line.</summary>
        Bubble,

        /// <summary>The cursor's bar, an element in the line.</summary>
        CursorBar,

        /// <summary>A line break or tab marker, an element in the line.</summary>
        BreakMarker,

        /// <summary>A value's icon, as a character of the icon font.</summary>
        ValueIcon,

        /// <summary>A value's label, in the interface font, as the bubble shows it.</summary>
        ValueLabel,

        /// <summary>Where the cursor ends up, as a bar character.</summary>
        CursorText,
    }
}
