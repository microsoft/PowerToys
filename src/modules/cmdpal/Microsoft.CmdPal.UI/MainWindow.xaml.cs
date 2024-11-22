// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using Windows.UI.WindowManagement;
using WinRT;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window,
    IRecipient<QuitMessage>
{
    private DesktopAcrylicController? _acrylicController;
    private SystemBackdropConfiguration? _configurationSource;

    public MainWindow()
    {
        InitializeComponent();

        PositionCentered();
        SetAcrylic();

        WeakReferenceMessenger.Default.Register<QuitMessage>(this);
        // Hide our titlebar.
        // We need to both ExtendsContentIntoTitleBar, then set the height to Collapsed
        // to hide the old caption buttons. Then, in UpdateRegionsForCustomTitleBar,
        // we'll make the top drag-able again. (after our content loads)
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        SizeChanged += WindowSizeChanged;
        RootShellPage.Loaded += RootShellPage_Loaded;
    }

    private void RootShellPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Now that our content has loaded, we can update our dragable regions
        UpdateRegionsForCustomTitleBar();
    }

    private void WindowSizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        UpdateRegionsForCustomTitleBar();
    }

    private void PositionCentered()
    {
        AppWindow.Resize(new SizeInt32 { Width = 860, Height = 560 });
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        if (displayArea is not null)
        {
            var centeredPosition = AppWindow.Position;
            centeredPosition.X = (displayArea.WorkArea.Width - AppWindow.Size.Width) / 2;
            centeredPosition.Y = (displayArea.WorkArea.Height - AppWindow.Size.Height) / 2;
            AppWindow.Move(centeredPosition);
        }
    }

    // We want to use DesktopAcrylicKind.Thin and custom colors as this is the default material
    // other Shell surfaces are using, this cannot be set in XAML however.
    private void SetAcrylic()
    {
        if (DesktopAcrylicController.IsSupported())
        {
            // Hooking up the policy object.
            _configurationSource = new SystemBackdropConfiguration
            {
                // Initial configuration state.
                IsInputActive = true,
            };
            UpdateAcrylic();
        }
    }

    private void UpdateAcrylic()
    {
        _acrylicController = GetAcrylicConfig(Content);

        // Enable the system backdrop.
        // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
        _acrylicController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
    }

    private static DesktopAcrylicController GetAcrylicConfig(UIElement content)
    {
        var feContent = content as FrameworkElement;

        return feContent?.ActualTheme == ElementTheme.Light
            ? new DesktopAcrylicController()
            {
                Kind = DesktopAcrylicKind.Thin,
                TintColor = Color.FromArgb(255, 243, 243, 243),
                LuminosityOpacity = 0.90f,
                TintOpacity = 0.0f,
                FallbackColor = Color.FromArgb(255, 238, 238, 238),
            }
            : new DesktopAcrylicController()
            {
                Kind = DesktopAcrylicKind.Thin,
                TintColor = Color.FromArgb(255, 32, 32, 32),
                LuminosityOpacity = 0.96f,
                TintOpacity = 0.5f,
                FallbackColor = Color.FromArgb(255, 28, 28, 28),
            };
    }

    private void DisposeAcrylic()
    {
        if (_acrylicController != null)
        {
            _acrylicController.Dispose();
            _acrylicController = null!;
            _configurationSource = null!;
        }
    }

    public void Receive(QuitMessage message)
    {
        Close();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        DisposeAcrylic();
    }

    // Updates our window s.t. the top of the window is dragable.
    private void UpdateRegionsForCustomTitleBar()
    {
        // Specify the interactive regions of the title bar.
        var scaleAdjustment = RootShellPage.XamlRoot.RasterizationScale;

        // Get the rectangle around our XAML content. We're going to mark this
        // rectangle as "Passthrough", so that the normal window operations
        // (resizing, dragging) don't apply in this space.
        var transform = RootShellPage.TransformToVisual(null);

        // Reserve 24px of space at the top for dragging.
        var bounds = transform.TransformBounds(new Rect(
            0,
            24,
            RootShellPage.ActualWidth,
            RootShellPage.ActualHeight));
        var contentRect = GetRect(bounds, scaleAdjustment);
        var rectArray = new RectInt32[] { contentRect };
        var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(this.AppWindow.Id);
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, rectArray);

        // Add a drag-able region on top
        var w = RootShellPage.ActualWidth;
        _ = RootShellPage.ActualHeight;
        var dragSides = new RectInt32[]
        {
            GetRect(new Rect(0, 0, w, 24), scaleAdjustment), // the top, 24 tall
        };
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Caption, dragSides);
    }

    private static RectInt32 GetRect(Rect bounds, double scale)
    {
        return new RectInt32(
            _X: (int)Math.Round(bounds.X * scale),
            _Y: (int)Math.Round(bounds.Y * scale),
            _Width: (int)Math.Round(bounds.Width * scale),
            _Height: (int)Math.Round(bounds.Height * scale));
    }
}
