// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using WinUIEx;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// CmdPal's transient toast banner. Inherits all of its chrome, click-through,
/// acrylic, and fade/slide animations from
/// <see cref="TransparentWindow"/>; adds only the bits that are bespoke to
/// CmdPal toasts: a bound message <c>TextBlock</c>, a 2.5 s auto-dismiss timer,
/// bottom-center positioning, and <see cref="QuitMessage"/> handling.
/// </summary>
public sealed partial class ToastWindow : TransparentWindow,
    IRecipient<QuitMessage>
{
    private static readonly TimeSpan VisibleDuration = TimeSpan.FromMilliseconds(2500);

    private readonly DispatcherQueueTimer _autoHideTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();

    public ToastViewModel ViewModel { get; } = new();

    public ToastWindow()
    {
        this.InitializeComponent();
        this.SetIcon();
        AppWindow.Title = RS_.GetString("ToastWindowTitle");
        this.SetWindowSize(600, 180);

        // Pin the chrome card to bottom-center with the toast's classic 560-wide
        // pill shape. The window itself stays 600x180 so the slide animations
        // have headroom and we don't have to chase SizeToContent.
        Card.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center;
        Card.VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Bottom;
        Card.MaxWidth = 560;
        Card.Margin = new Microsoft.UI.Xaml.Thickness(24, 24, 24, 16);

        WeakReferenceMessenger.Default.Register<QuitMessage>(this);
    }

    public void ShowToast(string message)
    {
        ViewModel.ToastMessage = message;

        DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Low,
            () =>
            {
                _autoHideTimer.Stop();
                PositionBottomCenter();
                Show();
                _autoHideTimer.Debounce(Hide, interval: VisibleDuration, immediate: false);
            });
    }

    public void Receive(QuitMessage message)
    {
        // This might come in on a background thread.
        DispatcherQueue.TryEnqueue(() => Close());
    }

    private void PositionBottomCenter()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        if (displayArea is null)
        {
            return;
        }

        var position = AppWindow.Position;
        position.X = displayArea.WorkArea.X + ((displayArea.WorkArea.Width - AppWindow.Size.Width) / 2);
        position.Y = displayArea.WorkArea.Y + displayArea.WorkArea.Height - AppWindow.Size.Height;
        AppWindow.Move(position);
    }
}
