// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// The visible "card" of the Command Palette — a control that renders the rounded
/// corners, border, shadow and system backdrop. The HWND that hosts it is borderless
/// and transparent, so all the chrome lives here instead of in window non-client area.
/// </summary>
public sealed partial class CmdPalMainControl : UserControl, IDisposable
{
    private readonly TintedControllerBackdrop _backdrop = new();
    private Color _cardFallbackBackground;

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
        _backdrop.BackdropAttachmentChanged += OnBackdropAttachmentChanged;
        BackdropElement.SystemBackdrop = _backdrop;
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
    /// When <paramref name="stretch"/> is <see langword="true"/>, the card stretches to fill
    /// the entire window vertically (non-compact mode). When <see langword="false"/>, the card
    /// sizes itself to its content and anchors to the top of the window (compact mode).
    /// </summary>
    public void SetCardStretch(bool stretch)
    {
        CardBorder.VerticalAlignment = stretch ? VerticalAlignment.Stretch : VerticalAlignment.Top;
    }

    /// <summary>
    /// Forwards the host window's activation state to the current backdrop so the system can
    /// render its active / inactive appearance correctly.
    /// </summary>
    public void SetIsInputActive(bool isActive)
    {
        _backdrop.IsInputActive = isActive;
    }

    /// <summary>
    /// Releases the active controller on the XAML thread while keeping the projected
    /// backdrop target rooted until WinUI disconnects it during shutdown.
    /// </summary>
    public void ClearBackdrop()
    {
        Dispose();
    }

    public void Dispose()
    {
        _backdrop.Dispose();
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
            // The border fill sits underneath SystemBackdropElement and remains a ready
            // fallback if a controller or composition brush cannot attach.
            _cardFallbackBackground = CreateCardBackground(backdrop, kind);
            SetCardBackground(_cardFallbackBackground);

            // Update the controller behind the one long-lived SystemBackdrop. Replacing the
            // SystemBackdrop property would create short-lived, thread-affine target
            // projections that C#/WinRT can otherwise release from its finalizer thread.
            _backdrop.Update(backdrop, kind, isImageMode, hasColorization);
            UpdateCardBackground(_backdrop.IsBackdropAttached);
        }
        catch (Exception ex)
        {
            SetCardBackground(backdrop.FallbackColor);
            Logger.LogError("Failed to apply backdrop to CmdPalMainControl", ex);
        }
    }

    private void SetCardBackground(Color color)
    {
        if (CardBorder.Background is SolidColorBrush background)
        {
            background.Color = color;
        }
        else
        {
            CardBorder.Background = new SolidColorBrush(color);
        }
    }

    private void OnBackdropAttachmentChanged(bool isBackdropAttached)
    {
        UpdateCardBackground(isBackdropAttached);
    }

    private void UpdateCardBackground(bool isBackdropAttached)
    {
        SetCardBackground(isBackdropAttached ? Colors.Transparent : _cardFallbackBackground);
    }

    private static Color CreateCardBackground(BackdropParameters backdrop, BackdropControllerKind kind)
    {
        if (kind == BackdropControllerKind.Solid)
        {
            return Color.FromArgb(
                (byte)(backdrop.EffectiveOpacity * 255),
                backdrop.TintColor.R,
                backdrop.TintColor.G,
                backdrop.TintColor.B);
        }

        return backdrop.FallbackColor;
    }
}
