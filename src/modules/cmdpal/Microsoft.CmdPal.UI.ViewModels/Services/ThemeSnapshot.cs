// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Represents a snapshot of theme-related visual settings, including accent color, theme preference, and background
/// image configuration, for use in rendering the Command Palette UI.
/// </summary>
public sealed class ThemeSnapshot
{
    /// <summary>
    /// Gets the accent tint color used by the Command Palette visuals.
    /// </summary>
    public required Color Tint { get; init; }

    /// <summary>
    /// Gets the accent tint color used by the Command Palette visuals.
    /// </summary>
    public required float TintIntensity { get; init; }

    /// <summary>
    /// Gets the configured application theme preference.
    /// </summary>
    public required ElementTheme Theme { get; init; }

    /// <summary>
    /// Gets the image source to render as the background, if any.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> when no background image is configured.
    /// </remarks>
    public required ImageSource? BackgroundImageSource { get; init; }

    /// <summary>
    /// Gets the stretch mode used to lay out the background image.
    /// </summary>
    public required Stretch BackgroundImageStretch { get; init; }

    /// <summary>
    /// Gets the opacity applied to the background image.
    /// </summary>
    /// <value>
    /// A value in the range [0, 1], where 0 is fully transparent and 1 is fully opaque.
    /// </value>
    public required double BackgroundImageOpacity { get; init; }

    /// <summary>
    /// Gets the effective acrylic backdrop parameters based on current settings and theme.
    /// </summary>
    /// <returns>The resolved <c>AcrylicBackdropParameters</c> to apply.</returns>
    public required AcrylicBackdropParameters BackdropParameters { get; init; }

    public required int BlurAmount { get; init; }

    public required float BackgroundBrightness { get; init; }
}
