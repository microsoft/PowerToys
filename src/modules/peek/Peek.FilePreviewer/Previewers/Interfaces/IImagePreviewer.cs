// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Media;
using Peek.FilePreviewer.Models;
using Windows.Foundation;

namespace Peek.FilePreviewer.Previewers.Interfaces
{
    /// <summary>
    /// Publishes coherent image-frame snapshots while retaining individual source and
    /// dimension properties for querying and binding.
    /// </summary>
    public interface IImagePreviewer : IPreviewer, IPreviewTarget, IReusablePreviewer
    {
        /// <summary>
        /// Gets the committed source/dimension snapshot, or null when no preview is
        /// available.
        /// </summary>
        /// <remarks>
        /// Read this property once when a coherent pair is required. A retained snapshot
        /// remains unchanged when another frame is committed. Rebinding preserves the
        /// previous frame until a replacement is committed or loading fails.
        /// </remarks>
        public ImagePreviewFrame? Frame { get; }

        /// <summary>
        /// Gets the current <see cref="Frame"/>'s source, or null when no preview is
        /// available.
        /// </summary>
        public ImageSource? Preview { get; }

        /// <summary>
        /// Gets the intrinsic dimensions of the committed <see cref="Preview"/> in
        /// physical pixels, or null when the dimensions are unknown.
        /// </summary>
        /// <remarks>
        /// These dimensions are not DPI-adjusted layout dimensions. Apply
        /// <see cref="ScalingFactor"/> when converting them to DIPs for presentation.
        /// This is a query projection of <see cref="Frame"/>, not independently updated
        /// sizing metadata. Source and size notifications remain separate, and separate
        /// getter calls may observe different commits. Use a single frame snapshot when
        /// source/dimension consistency is required.
        /// </remarks>
        public Size? ImageSize { get; }

        /// <summary>
        /// Gets or sets the positive display scale, expressed as physical pixels per DIP.
        /// </summary>
        public double ScalingFactor { get; set; }
    }
}
