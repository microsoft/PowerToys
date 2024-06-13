// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using MouseJumpUI.Common.Models.Drawing;

namespace MouseJumpUI.Common.Imaging;

internal interface IImageRegionCopyService
{
    /// <summary>
    /// Copies the source region from the provider's source image (e.g. the interactive desktop,
    /// a static image, etc) to the target region on the specified Graphics object.
    /// </summary>
    /// <remarks>
    /// Implementations of this interface are used to capture regions of the interactive desktop
    /// during runtime, or to capture regions of a static reference image during unit tests.
    /// </remarks>
    void CopyImageRegion(
        Graphics targetGraphics,
        RectangleInfo sourceBounds,
        RectangleInfo targetBounds);
}
