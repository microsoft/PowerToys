// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Configuration parameters for a backdrop style.
/// </summary>
public sealed record BackdropStyleConfig
{
    /// <summary>
    /// Gets the type of system backdrop controller to use.
    /// </summary>
    public required BackdropControllerKind ControllerKind { get; init; }

    /// <summary>
    /// Gets the base tint opacity before user adjustments.
    /// </summary>
    public required float BaseTintOpacity { get; init; }

    /// <summary>
    /// Gets the base luminosity opacity before user adjustments.
    /// </summary>
    public required float BaseLuminosityOpacity { get; init; }

    /// <summary>
    /// Gets the brush type to use for preview approximation.
    /// </summary>
    public required PreviewBrushKind PreviewBrush { get; init; }

    /// <summary>
    /// Gets the fixed opacity for styles that don't support user adjustment (e.g., Mica).
    /// When <see cref="SupportsOpacity"/> is false, this value is used as the effective opacity.
    /// </summary>
    public float FixedOpacity { get; init; }

    /// <summary>
    /// Gets whether this backdrop style supports custom colorization (tint colors).
    /// </summary>
    public bool SupportsColorization { get; init; } = true;

    /// <summary>
    /// Gets whether this backdrop style supports custom background images.
    /// </summary>
    public bool SupportsBackgroundImage { get; init; } = true;

    /// <summary>
    /// Gets whether this backdrop style supports opacity adjustment.
    /// </summary>
    public bool SupportsOpacity { get; init; } = true;

    /// <summary>
    /// Computes the effective tint opacity based on this style's configuration.
    /// </summary>
    /// <param name="userOpacity">User's backdrop opacity setting (0-1 normalized).</param>
    /// <param name="baseTintOpacityOverride">Optional override for base tint opacity (used by colorful theme).</param>
    /// <returns>The effective opacity to apply.</returns>
    public float ComputeEffectiveOpacity(float userOpacity, float? baseTintOpacityOverride = null)
    {
        // For styles that don't support opacity (Mica), use FixedOpacity
        if (!SupportsOpacity && FixedOpacity > 0)
        {
            return FixedOpacity;
        }

        // For Solid: only user opacity matters (controls alpha of solid color)
        if (ControllerKind == BackdropControllerKind.Solid)
        {
            return userOpacity;
        }

        // For blur effects: multiply base opacity with user opacity
        var baseTint = baseTintOpacityOverride ?? BaseTintOpacity;
        return baseTint * userOpacity;
    }
}
