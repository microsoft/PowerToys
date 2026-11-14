// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Determines how an image fits within a gallery tile.
/// </summary>
public enum GalleryImageFit
{
    /// <summary>
    /// Fills the tile while preserving aspect ratio and cropping overflow.
    /// </summary>
    Cover = 0,

    /// <summary>
    /// Fits the whole image while preserving aspect ratio.
    /// </summary>
    Contain = 1,
}
