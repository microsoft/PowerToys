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
/// CmdPal's transient toast notification. It is a bare
/// <see cref="TransparentWindow"/> host whose content is a
/// <see cref="Microsoft.PowerToys.Common.UI.Controls.TransientSurface"/> — the
/// surface supplies the acrylic, border, corners, shadow, and the fade/slide
/// animation, driven automatically off the window's show/hide events. This class
/// adds only the bits bespoke to CmdPal toasts: a bound message <c>TextBlock</c>,
/// a 2.5 s auto-dismiss timer, bottom-center positioning, and
/// <see cref="QuitMessage"/> handling.
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

        // Let the surface animate itself in/out in response to this window's
        // Show()/Hide(). The 600x180 window leaves the bottom-center 560-wide
        // pill (positioned in XAML) room for its slide + shadow.
        Surface.SubscribeTo(this);

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
