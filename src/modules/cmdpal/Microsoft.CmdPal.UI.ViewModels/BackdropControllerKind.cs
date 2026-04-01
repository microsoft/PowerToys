// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Specifies the type of system backdrop controller to use.
/// </summary>
public enum BackdropControllerKind
{
    /// <summary>
    /// Solid color with alpha transparency (TransparentTintBackdrop).
    /// </summary>
    Solid,

    /// <summary>
    /// Desktop Acrylic with default blur (DesktopAcrylicKind.Default).
    /// </summary>
    Acrylic,

    /// <summary>
    /// Desktop Acrylic with thinner blur (DesktopAcrylicKind.Thin).
    /// </summary>
    AcrylicThin,

    /// <summary>
    /// Mica effect (MicaKind.Base).
    /// </summary>
    Mica,

    /// <summary>
    /// Mica alternate/darker variant (MicaKind.BaseAlt).
    /// </summary>
    MicaAlt,

    /// <summary>
    /// Custom backdrop implementation.
    /// </summary>
    Custom,
}
