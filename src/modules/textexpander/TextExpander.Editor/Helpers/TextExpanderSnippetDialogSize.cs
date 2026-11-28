// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>
    /// The snippet dialog's width. The page fits the dialog's content to the window, up to what the dialog's widest,
    /// <see cref="MaxWidth"/>, leaves inside its border and padding. Content any wider is cut off at the dialog's right edge.
    /// </summary>
    internal static class TextExpanderSnippetDialogSize
    {
        /// <summary>The dialog's widest: room for Keyboard Manager's two columns. ContentDialogMaxWidth in the page's markup.</summary>
        public const double MaxWidth = 808;

        /// <summary>A content dialog's border (ContentDialogBorderWidth) and padding (ContentDialogPadding), on each side.</summary>
        public const double Frame = 1 + 24;

        /// <summary>The widest the content can be inside the dialog's frame.</summary>
        public const double MaxContentWidth = MaxWidth - (2 * Frame);

        /// <summary>The narrowest the content gets, however narrow the window.</summary>
        public const double MinContentWidth = 320;

        /// <summary>What the dialog leaves of the window's width around its content.</summary>
        public const double WindowMargin = 96;

        /// <summary>Gets the content's width in a window <paramref name="windowWidth"/> wide.</summary>
        public static double ContentWidth(double windowWidth) => Math.Clamp(windowWidth - WindowMargin, MinContentWidth, MaxContentWidth);
    }
}
