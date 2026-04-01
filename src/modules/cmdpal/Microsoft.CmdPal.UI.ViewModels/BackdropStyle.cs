// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Specifies the visual backdrop style for the window.
/// </summary>
public enum BackdropStyle
{
    /// <summary>
    /// Standard desktop acrylic with blur effect.
    /// </summary>
    Acrylic,

    /// <summary>
    /// Solid color with alpha transparency (no blur).
    /// </summary>
    Clear,

    /// <summary>
    /// Mica effect that samples the desktop wallpaper.
    /// </summary>
    Mica,

    /// <summary>
    /// Thinner acrylic variant with more transparency.
    /// </summary>
    AcrylicThin,

    /// <summary>
    /// Mica alternate variant (darker).
    /// </summary>
    MicaAlt,
}
