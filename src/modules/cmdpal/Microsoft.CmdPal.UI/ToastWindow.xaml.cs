// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI;

public sealed partial class ToastWindow : Window,
    IRecipient<QuitMessage>
{
    private readonly HWND _hwnd;

    public ToastViewModel ViewModel { get; } = new();

    private readonly DispatcherQueueTimer _debounceTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();

    public ToastWindow()
    {
        this.InitializeComponent();
        AppWindow.Hide();
        AppWindow.IsShownInSwitchers = false;
        ExtendsContentIntoTitleBar = true;
        AppWindow.SetPresenter(AppWindowPresenterKind.CompactOverlay);
        this.SetIcon();
        AppWindow.Title = RS_.GetString("ToastWindowTitle");
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

        _hwnd = new HWND(WinRT.Interop.WindowNative.GetWindowHandle(this).ToInt32());
        PInvoke.EnableWindow(_hwnd, false);

        WeakReferenceMessenger.Default.Register<QuitMessage>(this);
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
}
