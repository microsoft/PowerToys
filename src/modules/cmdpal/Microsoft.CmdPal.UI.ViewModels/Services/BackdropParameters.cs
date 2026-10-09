// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.UI;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Parameters for configuring the window backdrop appearance.
/// </summary>
/// <param name="TintColor">The tint color applied to the backdrop.</param>
/// <param name="FallbackColor">The fallback color when backdrop effects are unavailable.</param>
/// <param name="EffectiveOpacity">
/// The effective opacity for the backdrop, pre-computed by the theme provider.
/// For Acrylic style: TintOpacity * BackdropOpacity.
/// For Clear style: BackdropOpacity (controls the solid color alpha).
/// </param>
/// <param name="EffectiveLuminosityOpacity">
/// The effective luminosity opacity for Acrylic backdrop, pre-computed by the theme provider.
/// Computed as LuminosityOpacity * BackdropOpacity.
/// </param>
/// <param name="Style">The backdrop style (Acrylic or Clear).</param>
public sealed record BackdropParameters(
    Color TintColor,
    Color FallbackColor,
    float EffectiveOpacity,
    float EffectiveLuminosityOpacity,
    BackdropStyle Style = BackdropStyle.Acrylic);
