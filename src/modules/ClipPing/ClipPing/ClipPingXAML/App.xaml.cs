// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ClipPing.Overlays;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PowerToys.Interop;
using Windows.ApplicationModel.DataTransfer;

namespace ClipPing;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application, IDisposable
{
    private IOverlay? _overlay;
    private ETWTrace? _etwTrace = new ETWTrace();

    public App()
    {
        InitializeComponent();
    }

    public void Dispose()
    {
        _etwTrace?.Dispose();
        _etwTrace = null;
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

                var application = this;

                NativeEventWaiter.WaitForEventLoop(
                    Constants.ClipPingExitEvent(),
                    () =>
                    {
                        application._etwTrace?.Dispose();
                        application._etwTrace = null;
                        dispatcher.TryEnqueue(App.Current.Exit);
                    });
            }
        }
        else
        {
            Logger.LogInfo("ClipPing started detached from PowerToys Runner.");
        }

        PowerToysTelemetry.Log.WriteEvent(new Telemetry.ClipPingOpenedEvent());

        Clipboard.ContentChanged += Clipboard_ContentChanged;
        _overlay = LoadOverlay();
    }

    private void Clipboard_ContentChanged(object? sender, object e)
    {
        ShowOverlay();
    }

    private IOverlay LoadOverlay()
    {
        // TODO: Add a way to pick what overlay to use
        return new TopOverlay();
    }

    private void ShowOverlay()
    {
        var hwnd = NativeMethods.GetForegroundWindow();

        if (hwnd == IntPtr.Zero)
        {
            // No active window
            return;
        }

        // Get bounding rectangle in device coordinates
        var hr = NativeMethods.DwmGetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out NativeMethods.RECT rect,
            Marshal.SizeOf<NativeMethods.RECT>());

        if (hr != 0)
        {
            return;
        }

        int windowWidth = rect.Right - rect.Left;
        int windowHeight = rect.Bottom - rect.Top;

        if (windowWidth <= 0 || windowHeight <= 0)
        {
            return;
        }

        var dpi = NativeMethods.GetDpiForWindow(hwnd);
        double scale = 96.0 / dpi;

        _overlay!.Show(new(rect.Left, rect.Top, windowWidth * scale, windowHeight * scale));
    }
}
