// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;
using WinUIEx;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// The visible "card" of the Command Palette — a control that renders the rounded
/// corners, border, shadow and system backdrop. The HWND that hosts it is borderless
/// and transparent, so all the chrome lives here instead of in window non-client area.
/// </summary>
public sealed partial class CmdPalMainControl : UserControl
{
    public static readonly DependencyProperty MainContentProperty =
        DependencyProperty.Register(
            nameof(MainContent),
            typeof(object),
            typeof(CmdPalMainControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty BackgroundLayerProperty =
        DependencyProperty.Register(
            nameof(BackgroundLayer),
            typeof(object),
            typeof(CmdPalMainControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ShadowPaddingProperty =
        DependencyProperty.Register(
            nameof(ShadowPadding),
            typeof(Thickness),
            typeof(CmdPalMainControl),
            new PropertyMetadata(new Thickness(16)));

    public static readonly DependencyProperty CardCornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CardCornerRadius),
            typeof(CornerRadius),
            typeof(CmdPalMainControl),
            new PropertyMetadata(new CornerRadius(8)));

    /// <summary>
    /// Gets or sets the main UI content hosted inside the card (e.g. the ShellPage).
    /// </summary>
    public object? MainContent
    {
        get => GetValue(MainContentProperty);
        set => SetValue(MainContentProperty, value);
    }

    /// <summary>
    /// Gets or sets a background layer rendered between the backdrop and the main content
    /// (e.g. the BlurImageControl). Hit-testing is disabled on this layer.
    /// </summary>
    public object? BackgroundLayer
    {
        get => GetValue(BackgroundLayerProperty);
        set => SetValue(BackgroundLayerProperty, value);
    }

    /// <summary>
    /// Gets or sets the amount of transparent padding around the card. The drop shadow
    /// is rendered into this padded area.
    /// </summary>
    public Thickness ShadowPadding
    {
        get => (Thickness)GetValue(ShadowPaddingProperty);
        set => SetValue(ShadowPaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets the corner radius of the card. Applied to both the clipping border
    /// and the backdrop element.
    /// </summary>
    public CornerRadius CardCornerRadius
    {
        get => (CornerRadius)GetValue(CardCornerRadiusProperty);
        set => SetValue(CardCornerRadiusProperty, value);
    }

    /// <summary>
    /// Gets the visible card border. Drag regions should be computed against this element
    /// so they line up with what the user sees, not the (larger, transparent) HWND.
    /// </summary>
    public FrameworkElement CardElement => CardBorder;

    /// <summary>
    /// Gets the panel inside the card that hosts the backdrop, background layer, and main
    /// content. Overlay UI (e.g. the dev ribbon) can be added to this panel so it draws
    /// inside the rounded card.
    /// </summary>
    public Panel CardContentPanel => CardContent;

    public CmdPalMainControl()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Clamps the maximum height of the visible card (in DIPs). Use this to keep an expanded
    /// compact card from growing past the bottom of the display. Pass
    /// <see cref="double.PositiveInfinity"/> to remove the clamp.
    /// </summary>
    public void SetCardMaxHeight(double maxHeightDip)
    {
        CardBorder.MaxHeight = maxHeightDip;
    }

    /// <summary>
    /// Returns the current height of the visible card (in DIPs). When the card is in its
    /// compact layout this is the height of just the search box, which callers use to center
    /// the collapsed card on screen.
    /// </summary>
    public double GetCardHeight()
    {
        CardBorder.UpdateLayout();
        return CardBorder.ActualHeight;
    }

    /// <summary>
    /// Forwards the host window's activation state to the current backdrop so the system can
    /// render its active / inactive appearance correctly.
    /// </summary>
    public void SetIsInputActive(bool isActive)
    {
        if (BackdropElement.SystemBackdrop is TintedControllerBackdrop tinted)
        {
            tinted.IsInputActive = isActive;
        }
    }

    /// <summary>
    /// Detaches any backdrop from the embedded element. Used during shutdown to release the
    /// underlying controller eagerly.
    /// </summary>
    public void ClearBackdrop()
    {
        BackdropElement.SystemBackdrop = null;
    }

    /// <summary>
    /// Applies a backdrop configuration to the embedded <see cref="SystemBackdropElement"/>.
    /// </summary>
    /// <param name="backdrop">Tint / opacity / fallback parameters from the theme service.</param>
    /// <param name="kind">The controller kind selected by the user's backdrop style.</param>
    /// <param name="isImageMode">When true, the background image control draws the tint, so no tint is applied to the backdrop itself.</param>
    /// <param name="hasColorization">When true, custom tint properties are applied to Mica backdrops.</param>
    public void ApplyBackdrop(BackdropParameters backdrop, BackdropControllerKind kind, bool isImageMode, bool hasColorization)
    {
        try
        {
            BackdropElement.SystemBackdrop = CreateBackdrop(backdrop, kind, isImageMode, hasColorization);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to apply backdrop to CmdPalMainControl", ex);
        }
    }

    private static Microsoft.UI.Xaml.Media.SystemBackdrop? CreateBackdrop(BackdropParameters backdrop, BackdropControllerKind kind, bool isImageMode, bool hasColorization)
    {
        // Image mode: don't tint here, BlurImageControl handles it (avoids double-tinting).
        var effectiveTintOpacity = isImageMode ? 0.0f : backdrop.EffectiveOpacity;

        switch (kind)
        {
            case BackdropControllerKind.Solid:
                var solidTint = Color.FromArgb(
                    (byte)(backdrop.EffectiveOpacity * 255),
                    backdrop.TintColor.R,
                    backdrop.TintColor.G,
                    backdrop.TintColor.B);
                return new TransparentTintBackdrop { TintColor = solidTint };

            case BackdropControllerKind.Mica:
            case BackdropControllerKind.MicaAlt:
                if (!MicaController.IsSupported())
                {
                    return new TransparentTintBackdrop { TintColor = backdrop.FallbackColor };
                }

                return new TintedMicaBackdrop
                {
                    Kind = kind == BackdropControllerKind.MicaAlt ? MicaKind.BaseAlt : MicaKind.Base,
                    ApplyTint = hasColorization || isImageMode,
                    TintColor = backdrop.TintColor,
                    TintOpacity = effectiveTintOpacity,
                    FallbackColor = backdrop.FallbackColor,
                    LuminosityOpacity = backdrop.EffectiveLuminosityOpacity,
                };

            case BackdropControllerKind.Acrylic:
            case BackdropControllerKind.AcrylicThin:
            default:
                if (!DesktopAcrylicController.IsSupported())
                {
                    return new TransparentTintBackdrop { TintColor = backdrop.FallbackColor };
                }

                return new TintedDesktopAcrylicBackdrop
                {
                    Kind = kind == BackdropControllerKind.AcrylicThin
                        ? DesktopAcrylicKind.Thin
                        : DesktopAcrylicKind.Default,
                    TintColor = backdrop.TintColor,
                    TintOpacity = effectiveTintOpacity,
                    FallbackColor = backdrop.FallbackColor,
                    LuminosityOpacity = backdrop.EffectiveLuminosityOpacity,
                };
        }
    }
}
