// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Decides when a line that WinUI may trim shows as text. WinUI draws an inline element that
    /// falls past the ellipsis at the start of the line, over the text, so a line with bubbles or
    /// the cursor bar switches to text once it's trimmed. It tries them again only when it has
    /// clearly more room than when it was trimmed, so a window being resized doesn't flicker.
    /// </summary>
    internal sealed class TextExpanderTrimFallback
    {
        /// <summary>How much wider, in DIPs, a line must get before it tries its bubbles again.</summary>
        internal const double RetryWidthStep = 32;

        private double _trimmedWidth;

        /// <summary>Gets a value indicating whether the line shows as text.</summary>
        public bool ShowsText { get; private set; }

        /// <summary>Starts over with bubbles, for new content.</summary>
        public void Reset()
        {
            ShowsText = false;
        }

        /// <summary>
        /// Gets a value indicating whether the line has to be drawn again as text: it's trimmed
        /// and has something that isn't text.
        /// </summary>
        public bool OnTrimmed(bool isTrimmed, bool hasElements, double width)
        {
            if (ShowsText || !isTrimmed || !hasElements)
            {
                return false;
            }

            ShowsText = true;
            _trimmedWidth = width;
            return true;
        }

        /// <summary>
        /// Gets a value indicating whether the line has to be drawn again with its bubbles, now that
        /// it has clearly more room than when it was trimmed. If they're still cut off, it's trimmed
        /// again and goes back to text.
        /// </summary>
        public bool OnWidthChanged(double width)
        {
            if (!ShowsText || width < _trimmedWidth + RetryWidthStep)
            {
                return false;
            }

            ShowsText = false;
            return true;
        }
    }
}
