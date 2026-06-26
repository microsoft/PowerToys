// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Common.UI.Controls;

/// <summary>
/// The edge a surface (e.g. <see cref="TransientSurface"/>) slides in from — and
/// out toward — when it is shown or hidden.
/// </summary>
public enum SlideDirection
{
    /// <summary>No animation; the surface appears and disappears instantly.</summary>
    None,

    /// <summary>Slide in from the left edge.</summary>
    Left,

    /// <summary>Slide in from the top edge.</summary>
    Top,

    /// <summary>Slide in from the right edge.</summary>
    Right,

    /// <summary>Slide in from the bottom edge.</summary>
    Bottom,
}
