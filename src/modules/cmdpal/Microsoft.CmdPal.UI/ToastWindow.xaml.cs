// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerToys.Common.UI.Controls;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Windows.Graphics;
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
/// a 2.5 s auto-dismiss timer, settings-driven positioning (bottom center by
/// default), and <see cref="QuitMessage"/> handling.
/// </summary>
public sealed partial class ToastWindow : TransparentWindow,
    IRecipient<QuitMessage>
{
    private static readonly TimeSpan VisibleDuration = TimeSpan.FromMilliseconds(2500);

    // Toasts carrying an action button stay up longer so there's time to click it.
    private static readonly TimeSpan VisibleDurationWithCommand = TimeSpan.FromMilliseconds(5000);

    private readonly DispatcherQueueTimer _autoHideTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();

    private TimeSpan _visibleDuration = VisibleDuration;

    public ToastViewModel ViewModel { get; } = new();

    public ToastWindow()
    {
        this.InitializeComponent();
        this.SetIcon();
        AppWindow.Title = RS_.GetString("ToastWindowTitle");
        this.SetWindowSize(600, 180);

        // Let the surface animate itself in/out in response to this window's
        // Show()/Hide(). The 600x180 window leaves the 560-wide pill (anchored
        // in PositionWindow) room for its slide + shadow.
        Surface.SubscribeTo(this);

        // Don't auto-hide out from under a pointer that's heading for the action button.
        Surface.PointerEntered += (_, _) => _autoHideTimer.Stop();
        Surface.PointerExited += (_, _) => _autoHideTimer.Debounce(Hide, interval: _visibleDuration, immediate: false);

        WeakReferenceMessenger.Default.Register<QuitMessage>(this);
    }

    public void ShowToast(ShowToastMessage toast)
    {
        ViewModel.ToastMessage = toast.Message;
        ViewModel.Icon = toast.Icon;
        ViewModel.Command = toast.Command;

        _visibleDuration = toast.Command is not null ? VisibleDurationWithCommand : VisibleDuration;

        DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Low,
            () =>
            {
                _autoHideTimer.Stop();
                PositionWindow(App.Current.Services.GetRequiredService<ISettingsService>().Settings.ToastPosition);
                Show();
                _autoHideTimer.Debounce(Hide, interval: _visibleDuration, immediate: false);
            });
    }

    private void CommandButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var command = ViewModel.Command;
        if (command is null)
        {
            return;
        }

        _autoHideTimer.Stop();
        Hide();

        // ShowWindowIfPage summons the palette when the command is a page.
        WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(command.Model) { ShowWindowIfPage = true });
    }

    public void Receive(QuitMessage message)
    {
        // This might come in on a background thread.
        DispatcherQueue.TryEnqueue(() => Close());
    }

    private void PositionWindow(ToastPosition toastPosition)
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        if (displayArea is null)
        {
            return;
        }

        var workArea = displayArea.WorkArea;
        var centeredX = workArea.X + ((workArea.Width - AppWindow.Size.Width) / 2);
        var bottomY = workArea.Y + workArea.Height - AppWindow.Size.Height;

        // The pill hugs the screen-adjacent window edges (16px gap) and keeps
        // 24px on the free edges for its slide + shadow; it slides in from the
        // screen edge it sits against.
        var (windowPosition, horizontal, vertical, margin, transition) = ResolveSystemPosition(toastPosition) switch
        {
            ToastPosition.TopLeft => (new PointInt32(workArea.X, workArea.Y), HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(16, 16, 24, 24), Transition.Top),
            ToastPosition.TopCenter => (new PointInt32(centeredX, workArea.Y), HorizontalAlignment.Center, VerticalAlignment.Top, new Thickness(24, 16, 24, 24), Transition.Top),
            _ => (new PointInt32(centeredX, bottomY), HorizontalAlignment.Center, VerticalAlignment.Bottom, new Thickness(24, 24, 24, 16), Transition.Bottom),
        };

        AppWindow.Move(windowPosition);
        Surface.HorizontalAlignment = horizontal;
        Surface.VerticalAlignment = vertical;
        Surface.Margin = margin;
        Surface.ShowTransition = transition;
        Surface.HideTransition = transition;
    }

    // Maps UseSystemSettings to the Windows "Position of on-screen indicators"
    // setting. Its PositionIndex values differ from ToastPosition.
    private static ToastPosition ResolveSystemPosition(ToastPosition toastPosition)
    {
        if (toastPosition != ToastPosition.UseSystemSettings)
        {
            return toastPosition;
        }

        try
        {
            return Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SystemSettings\ConfirmatorPosition",
                "PositionIndex",
                null) switch
            {
                3 => ToastPosition.TopCenter,
                2 => ToastPosition.TopLeft,
                _ => ToastPosition.BottomCenter,
            };
        }
        catch (Exception)
        {
            return ToastPosition.BottomCenter;
        }
    }
}
