// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using ScreenTranslator.Helpers;
using ScreenTranslator.Keyboard;
using WindowManager = ScreenTranslator.Helpers.WindowManager;

namespace ScreenTranslator;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Application manages lifecycle via process exit")]
public partial class App : Application
{
    private static Mutex? _instanceMutex;
    private static EventMonitor? _eventMonitor;
    private static KeyboardMonitor? _keyboardMonitor;
    private static LifetimeWindow? _lifetimeWindow;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _instanceMutex = new Mutex(true, "PowerToys_ScreenTranslator_InstanceMutex", out bool isNewInstance);
        if (!isNewInstance)
        {
            Logger.LogWarning("Another instance of ScreenTranslator is already running. Exiting.");
            Exit();
            return;
        }

        var cmdArgs = Environment.GetCommandLineArgs();
        if (cmdArgs?.Length > 1)
        {
            if (int.TryParse(cmdArgs[cmdArgs.Length - 1], out int powerToysRunnerPid))
            {
                var dispatcher = DispatcherQueue.GetForCurrentThread();
                RunnerHelper.WaitForPowerToysRunner(powerToysRunnerPid, () =>
                {
                    dispatcher.TryEnqueue(Shutdown);
                });
            }
        }

        _eventMonitor = new EventMonitor();
        _eventMonitor.Start();

        _keyboardMonitor = new KeyboardMonitor();
        _keyboardMonitor.Start();

        // Keep the WinUI dispatcher alive while the utility waits for its activation event.
        _lifetimeWindow = new LifetimeWindow();

        AppDomain.CurrentDomain.ProcessExit += (s, e) => Cleanup();

        Logger.LogInfo("ScreenTranslator WinUI 3 application initialized and listening for activation events.");
    }

    private static void Cleanup()
    {
        try
        {
            WindowManager.CloseAllOverlays();

            _lifetimeWindow?.Close();
            _lifetimeWindow = null;

            _eventMonitor?.Dispose();
            _eventMonitor = null;

            _keyboardMonitor?.Dispose();
            _keyboardMonitor = null;

            if (_instanceMutex != null)
            {
                try
                {
                    _instanceMutex.ReleaseMutex();
                }
                catch (ApplicationException ex)
                {
                    Logger.LogWarning($"ScreenTranslator instance mutex was not owned during cleanup: {ex.Message}");
                }

                _instanceMutex.Dispose();
                _instanceMutex = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Exception during cleanup: {ex.Message}");
        }
    }

    public static void Shutdown()
    {
        Cleanup();
        Current?.Exit();
    }
}
