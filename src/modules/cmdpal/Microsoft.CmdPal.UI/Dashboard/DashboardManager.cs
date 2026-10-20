// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI.Dispatching;

namespace Microsoft.CmdPal.UI.Dashboard;

/// <summary>
/// Owns the dashboard window: creates it on first use, toggles it from the hotkey, keeps it
/// in sync with settings, and moves it when the primary display changes.
/// </summary>
public sealed partial class DashboardManager :
    IRecipient<ToggleDashboardMessage>,
    IRecipient<HideDashboardMessage>,
    IRecipient<QuitMessage>,
    IDisposable
{
    private static readonly TimeSpan MonitorsChangedDebounceInterval = TimeSpan.FromMilliseconds(500);

    private readonly IMonitorService _monitorService;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _monitorsChangedDebounceTimer;

    private DashboardWindow? _window;
    private DashboardSettings? _appliedSettings;
    private bool _disposed;

    public DashboardManager(
        IMonitorService monitorService,
        ISettingsService settingsService,
        DispatcherQueue dispatcherQueue)
    {
        _monitorService = monitorService;
        _settingsService = settingsService;
        _dispatcherQueue = dispatcherQueue;
        _monitorsChangedDebounceTimer = _dispatcherQueue.CreateTimer();

        _monitorService.MonitorsChanged += OnMonitorsChanged;
        _settingsService.SettingsChanged += OnSettingsChanged;

        WeakReferenceMessenger.Default.Register<ToggleDashboardMessage>(this);
        WeakReferenceMessenger.Default.Register<HideDashboardMessage>(this);
        WeakReferenceMessenger.Default.Register<QuitMessage>(this);
    }

    public void Receive(ToggleDashboardMessage message) =>
        _dispatcherQueue.TryEnqueue(() => Toggle(message.FromHotkey));

    public void Receive(HideDashboardMessage message) =>
        _dispatcherQueue.TryEnqueue(() => _window?.HideDashboard());

    public void Receive(QuitMessage message) =>
        _dispatcherQueue.TryEnqueue(CloseWindow);

    private void Toggle(bool fromHotkey)
    {
        if (_disposed)
        {
            return;
        }

        var settings = _settingsService.Settings;
        if (!settings.EnableDashboard)
        {
            return;
        }

        var isShown = _window?.IsShown == true;
        if (fromHotkey && !isShown && settings.DashboardSettings.IgnoreHotkeyWhenFullscreen &&
            WindowHelper.GetUserNotificationFlags().IsFullscreenState)
        {
            Logger.LogDebug("Ignoring the dashboard hotkey while a fullscreen app is running.");
            return;
        }

        try
        {
            EnsureWindow(settings.DashboardSettings).ToggleDashboard();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to toggle the dashboard.", ex);
        }
    }

    private DashboardWindow EnsureWindow(DashboardSettings settings)
    {
        if (_window is null)
        {
            _window = new DashboardWindow(settings);
            _window.Closed += Window_Closed;
            _appliedSettings = settings;
        }

        return _window;
    }

    private void Window_Closed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
    {
        if (ReferenceEquals(sender, _window))
        {
            _window = null;
            _appliedSettings = null;
        }
    }

    private void CloseWindow()
    {
        var window = _window;
        _window = null;
        _appliedSettings = null;
        if (window is not null)
        {
            window.Closed -= Window_Closed;
            window.Close();
        }
    }

    private void OnSettingsChanged(ISettingsService sender, SettingsModel settings)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed || _window is null)
            {
                return;
            }

            if (!settings.EnableDashboard)
            {
                CloseWindow();
                return;
            }

            if (!Equals(_appliedSettings, settings.DashboardSettings))
            {
                _appliedSettings = settings.DashboardSettings;
                _window.ApplySettings(settings.DashboardSettings);
            }
        });
    }

    // The dashboard lays itself out from its window size, so moving the window to the new
    // primary display bounds is enough to rearrange the tiles.
    private void OnMonitorsChanged(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed)
            {
                return;
            }

            _monitorsChangedDebounceTimer.Debounce(
                () =>
                {
                    if (_window?.IsShown == true)
                    {
                        _window.UpdateBounds();
                    }
                },
                interval: MonitorsChangedDebounceInterval,
                immediate: false);
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _monitorService.MonitorsChanged -= OnMonitorsChanged;
        _settingsService.SettingsChanged -= OnSettingsChanged;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _monitorsChangedDebounceTimer.Stop();
        CloseWindow();
    }
}
