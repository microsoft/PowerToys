// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Models
{
    /// <summary>
    /// Keeps the displayed and staged frames separate so preparing a replacement does not
    /// change the visible image's source or intrinsic bounds.
    /// </summary>
    /// <remarks>
    /// Tracks frame metadata only; the control owns the image elements and their visibility.
    /// </remarks>
    internal sealed class ImagePreviewBuffer
    {
        /// <summary>
        /// Gets the displayed frame, or null before the first swap or after clearing.
        /// </summary>
        public ImagePreviewFrame? Current { get; private set; }

        /// <summary>
        /// Gets the prepared frame awaiting presentation, or null when none is staged.
        /// </summary>
        public ImagePreviewFrame? Next { get; private set; }

        /// <summary>
        /// Replaces the staged frame without changing the displayed frame.
        /// </summary>
        /// <param name="frame">The producer's coherent source/dimension snapshot to
        /// stage.</param>
        public void Prepare(ImagePreviewFrame frame) => Next = frame;

        /// <summary>
        /// Promotes the staged source and its dimensions together, then releases the
        /// previous frame.
        /// </summary>
        /// <returns>True if a frame was promoted; false if no replacement was staged.
        /// </returns>
        public bool Swap()
        {
            if (Next is null)
            {
                return false;
            }

            Current = Next;
            Next = null;
            return true;
        }

        /// <summary>
        /// Releases both frames so no previous source or bounds survive a reset.
        /// </summary>
        public void Clear()
        {
            Current = null;
            Next = null;
        }
    }
}
