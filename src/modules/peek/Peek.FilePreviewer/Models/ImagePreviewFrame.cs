// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Peek.FilePreviewer.Models
{
    /// <summary>
    /// Pairs a loaded image source with its intrinsic dimensions so size limits follow
    /// the correct source when buffers are staged and swapped.
    /// </summary>
    /// <remarks>
    /// Retaining this snapshot preserves the source/dimension pairing even when the
    /// previewer publishes another frame. It does not make the image source itself
    /// immutable or synchronize native window resizing with graphics presentation.
    /// </remarks>
    /// <param name="Source">The loaded source to present.</param>
    /// <param name="PixelSize">Intrinsic dimensions in physical pixels, or null when
    /// unknown.</param>
    public sealed record ImagePreviewFrame(ImageSource Source, Size? PixelSize)
    {
        /// <summary>
        /// Converts intrinsic dimensions to layout limits that prevent upscaling on the
        /// current display.
        /// </summary>
        /// <param name="scalingFactor">The positive display scale, expressed as physical
        /// pixels per DIP.</param>
        /// <returns>Maximum dimensions in DIPs, or unbounded dimensions when the pixel
        /// size is unknown.</returns>
        public Size GetMaxSize(double scalingFactor) => PixelSize is Size size
            ? new Size(size.Width / scalingFactor, size.Height / scalingFactor)
            : new Size(double.PositiveInfinity, double.PositiveInfinity);
    }
}
