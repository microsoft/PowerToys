﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ClipPing.Overlays;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PowerToys.Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace ClipPing;

public partial class App : Application, IDisposable
{
    private static readonly SettingsUtils ModuleSettings = new();
    private readonly FileSystemWatcher _fileSystemWatcher;
    private readonly ETWTrace _etwTrace = new();
    private ClipPingSettings _currentSettings;
    private IOverlay? _overlay;

    private static readonly Dictionary<ClipPingOverlay, Type> OverlayTypes = new()
    {
        { ClipPingOverlay.Top, typeof(TopOverlay) },
        { ClipPingOverlay.Border, typeof(BorderOverlay) },
    };

    public App()
    {
        InitializeComponent();

        _currentSettings = ModuleSettings.GetSettingsOrDefault<ClipPingSettings>(ClipPingSettings.ModuleName);

        var settingsPath = ModuleSettings.GetSettingsFilePath(ClipPingSettings.ModuleName);

        _fileSystemWatcher = new FileSystemWatcher
        {
            Path = Path.GetDirectoryName(settingsPath) ?? string.Empty,
            Filter = Path.GetFileName(settingsPath),
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
        };

        _fileSystemWatcher.Changed += Settings_Changed;
        _fileSystemWatcher.EnableRaisingEvents = true;
    }

    protected Type OverlayType
    {
        get
        {
            if (OverlayTypes.TryGetValue(_currentSettings.Properties.OverlayType, out var overlayType))
            {
                return overlayType;
            }

            Logger.LogWarning($"Unknown overlay type: {_currentSettings.Properties.OverlayType}. Defaulting to TopOverlay.");
            return typeof(TopOverlay);
        }
    }

    private void Settings_Changed(object sender, FileSystemEventArgs e)
    {
        _ = OnSettingsChanged();
    }

    private async Task OnSettingsChanged()
    {
        await Task.Delay(25);

        try
        {
            _currentSettings = ModuleSettings.GetSettings<ClipPingSettings>(ClipPingSettings.ModuleName);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load ClipPing settings: {ex}.");
        }
    }

    public void Dispose()
    {
        _fileSystemWatcher.Dispose();
        _etwTrace.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var cmdArgs = Environment.GetCommandLineArgs();
        if (cmdArgs.Length > 1)
        {
            if (int.TryParse(cmdArgs[^1], out var powerToysRunnerPid))
            {
                Logger.LogInfo($"ClipPing started from the PowerToys Runner. Runner pid={powerToysRunnerPid}.");

                var dispatcher = DispatcherQueue.GetForCurrentThread();
                RunnerHelper.WaitForPowerToysRunner(powerToysRunnerPid, () =>
                {
                    Logger.LogInfo("PowerToys Runner exited. Exiting ClipPing");
                    dispatcher.TryEnqueue(App.Current.Exit);
                });

                NativeEventWaiter.WaitForEvents(
                    (Constants.ClipPingExitEvent(), ExitEventSignaled),
                    (Constants.ClipPingShowOverlayEvent(), ShowOverlay));
            }
        }
        else
        {
            Logger.LogInfo("ClipPing started detached from PowerToys Runner.");
        }

        PowerToysTelemetry.Log.WriteEvent(new Telemetry.ClipPingOpenedEvent());

        Clipboard.ContentChanged += Clipboard_ContentChanged;
        _ = GetOverlay(); // Preload the overlay to avoid delays when showing it.
    }

    private void ExitEventSignaled()
    {
        _etwTrace.Dispose();
        App.Current.Exit();
    }

    private void Clipboard_ContentChanged(object? sender, object e)
    {
        ShowOverlay();
    }

    private IOverlay? GetOverlay()
    {
        if (_overlay?.GetType() != OverlayType)
        {
            var oldOverlay = _overlay;

            if (oldOverlay != null)
            {
                // No clue why, WinUI crashes if we close the old overlay right away.
                // Enqueue it so that it gets closed on the next message pump.
                DispatcherQueue.GetForCurrentThread().TryEnqueue(() => oldOverlay.Dispose());
            }

            _overlay = Activator.CreateInstance(OverlayType) as IOverlay;
        }

        return _overlay;
    }

    private void ShowOverlay()
    {
        var hwnd = NativeMethods.GetForegroundWindow();

        if (hwnd == IntPtr.Zero)
        {
            Logger.LogInfo("Overlay hidden because there is no active window.");
            return;
        }

        // Get bounding rectangle in device coordinates
        var hr = NativeMethods.DwmGetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out var rect,
            Marshal.SizeOf<NativeMethods.RECT>());

        if (hr != 0)
        {
            Logger.LogWarning($"Could not get the foreground window attributes (hwnd: {hwnd:x2}, hr: {hr:x2}).");
            return;
        }

        int windowWidth = rect.Right - rect.Left;
        int windowHeight = rect.Bottom - rect.Top;

        if (windowWidth <= 0 || windowHeight <= 0)
        {
            Logger.LogWarning($"The foreground window has zero or negative size: {rect}.");
            return;
        }

        uint dpi = 96;

        var awareness = NativeMethods.GetAwarenessFromDpiAwarenessContext(NativeMethods.GetWindowDpiAwarenessContext(hwnd));
        bool isDpiAware = awareness is NativeMethods.DPI_AWARENESS.PER_MONITOR_AWARE;

        // If the window is DPI unaware, we need to adjust the scale based on the monitor DPI
        if (isDpiAware)
        {
            dpi = NativeMethods.GetDpiForWindow(hwnd);
        }
        else
        {
            var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);

            if (NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var monDpiX, out _) == 0)
            {
                dpi = monDpiX;
            }
        }

        double scale = 96.0 / dpi;

        var rawColor = _currentSettings.Properties.OverlayColor;

        // Convert #RRGGBB to Windows.UI.Color
        var color = Windows.UI.Color.FromArgb(
            255,
            Convert.ToByte(rawColor.Value.Substring(1, 2), 16),
            Convert.ToByte(rawColor.Value.Substring(3, 2), 16),
            Convert.ToByte(rawColor.Value.Substring(5, 2), 16));

        var target = new Rect(rect.Left, rect.Top, windowWidth * scale, windowHeight * scale);

        Logger.LogDebug($"Showing overlay at {target} with color {color}.");

        GetOverlay()?.Show(target, color);
    }
}
