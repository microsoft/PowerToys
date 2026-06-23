// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Common.UI.Controls;

/// <summary>
/// A floating "card" surface for transient PowerToys overlays (toasts,
/// banners, indicators). Provides the PowerToys-standard chrome — 1 px
/// border in <c>SurfaceStrokeColorDefaultBrush</c>, 8 px corner radius,
/// a <c>ThemeShadow</c>, and an always-active desktop acrylic backdrop —
/// via a default <see cref="Microsoft.UI.Xaml.Controls.ControlTemplate"/>.
/// </summary>
/// <remarks>
/// Lives inside a <see cref="Window.TransparentWindow"/>. Apps that want a
/// different look supply their own <c>Style TargetType="TransparentCard"</c>
/// in resources — the standard WinUI restyle path.
/// </remarks>
public sealed partial class TransparentCard : ContentControl
{
    public TransparentCard()
    {
        DefaultStyleKey = typeof(TransparentCard);
    }
}
