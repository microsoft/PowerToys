// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.UI;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI;

public sealed partial class ToastWindow : Window,
    IRecipient<QuitMessage>
{
    private readonly HWND _hwnd;

    public ToastViewModel ViewModel { get; } = new();

    private readonly DispatcherQueueTimer _debounceTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();

    private DesktopAcrylicController? _acrylicController;
    private SystemBackdropConfiguration? _configurationSource;

    public ToastWindow()
    {
        this.InitializeComponent();
        AppWindow.Hide();
        AppWindow.IsShownInSwitchers = false;
        ExtendsContentIntoTitleBar = true;
        AppWindow.SetPresenter(AppWindowPresenterKind.CompactOverlay);
        AppWindow.SetIcon("ms-appx:///Assets/Icons/StoreLogo.png");
        AppWindow.Title = RS_.GetString("ToastWindowTitle");
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

        _hwnd = new HWND(WinRT.Interop.WindowNative.GetWindowHandle(this).ToInt32());
        PInvoke.EnableWindow(_hwnd, false);

        WeakReferenceMessenger.Default.Register<QuitMessage>(this);

        SetAcrylic();
        ToastText.ActualThemeChanged += (s, e) => UpdateAcrylic();
    }

    private void PositionCentered()
    {
        var intSize = new SizeInt32
        {
            Width = Convert.ToInt32(ToastText.ActualWidth),
            Height = Convert.ToInt32(ToastText.ActualHeight),
        };
        AppWindow.Resize(intSize);

        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        if (displayArea is not null)
        {
            var centeredPosition = AppWindow.Position;
            centeredPosition.X = (displayArea.WorkArea.Width - AppWindow.Size.Width) / 2;

            var monitorHeight = displayArea.WorkArea.Height;
            var windowHeight = AppWindow.Size.Height;
            centeredPosition.Y = monitorHeight - (windowHeight * 2);
            AppWindow.Move(centeredPosition);
        }
    }

    public void ShowToast(string message)
    {
        ViewModel.ToastMessage = message;
        DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Low,
            () =>
        {
            PositionCentered();

            // SW_SHOWNA prevents us from getting activated (and stealing FG)
            PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_SHOWNA);

            _debounceTimer.Debounce(
                () =>
                {
                    AppWindow.Hide();
                },
                interval: TimeSpan.FromMilliseconds(2500),
                immediate: false);
        });
    }

    public void Receive(QuitMessage message)
    {
        // This might come in on a background thread
        DispatcherQueue.TryEnqueue(() => Close());
    }

    ////// Literally everything below here is for acrylic //////

    internal void OnClosed(object sender, WindowEventArgs args) => DisposeAcrylic();

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

    internal void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_configurationSource != null)
        {
            _configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }
    }
}
