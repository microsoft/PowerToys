// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

using MouseJump.Models.Drawing;

namespace MouseJump.Common.Capture;

/// <summary>
/// Represents a screenshot capture implementation for a specific device. A caller may issue
/// several <see cref="CaptureAsync"/> requests (one per screen on the device) without awaiting
/// each in turn - it's up to the provider to decide whether to service them in parallel or in
/// series, depending on what its underlying capture mechanism can actually support.
/// </summary>
/// <remarks>
/// Implementations of this interface are used to capture regions of the interactive desktop
/// during runtime, or to capture regions of a static reference image during unit tests.
/// </remarks>
public interface IScreenshotCaptureProvider
{
    /// <summary>
    /// Captures <paramref name="sourceArea"/> and returns it as a new bitmap scaled to
    /// <paramref name="thumbnailSize"/>. If <paramref name="cancellationToken"/> is already
    /// cancelled before this request starts, it's abandoned without ever running.
    /// </summary>
    Task<Bitmap> CaptureAsync(
        RectangleInfo sourceArea,
        SizeInfo thumbnailSize,
        CancellationToken cancellationToken = default);
}
