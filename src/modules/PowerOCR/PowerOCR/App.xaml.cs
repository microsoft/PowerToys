// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PowerOCR.Helpers;
using PowerOCR.Keyboard;
using PowerOCR.Settings;
using PowerToys.Interop;

namespace PowerOCR;

public partial class App : Application, IDisposable
{
    private static DispatcherQueue? _dispatcherQueue;

    public static DispatcherQueue? DispatcherQueueInstance => _dispatcherQueue;

    private KeyboardMonitor? keyboardMonitor;
    private EventMonitor? eventMonitor;
    private Mutex? _instanceMutex;
    private int _powerToysRunnerPid;
    private ETWTrace etwTrace = new ETWTrace();
    private static readonly List<OCROverlay> _overlays = new();

    private CancellationTokenSource NativeThreadCTS { get; set; }

    public App()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        Logger.InitializeLogger("\\TextExtractor\\Logs");

        try
        {
            string appLanguage = ManagedCommon.LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(appLanguage))
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(appLanguage);
            }
        }
        catch (CultureNotFoundException ex)
        {
            Logger.LogError("CultureNotFoundException: " + ex.Message);
        }

        NativeThreadCTS = new CancellationTokenSource();

        NativeEventWaiter.WaitForEventLoop(
            Constants.TerminatePowerOCRSharedEvent(),
            () => Exit(),
            NativeThreadCTS.Token);

        InitializeComponent();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        keyboardMonitor?.Dispose();
        etwTrace?.Dispose();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredTextExtractorEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
        {
            Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
            Exit();
            return;
        }

        // allow only one instance of PowerOCR
        _instanceMutex = new Mutex(true, @"Local\PowerToys_PowerOCR_InstanceMutex", out bool createdNew);
        if (!createdNew)
        {
            Logger.LogWarning("Another running TextExtractor instance was detected. Exiting TextExtractor");
            _instanceMutex = null;
            Exit();
            return;
        }

        string[] cmdArgs = Environment.GetCommandLineArgs();
        if (cmdArgs.Length > 1)
        {
            try
            {
                _ = int.TryParse(cmdArgs[1], out _powerToysRunnerPid);
                Logger.LogInfo($"TextExtractor started from the PowerToys Runner. Runner pid={_powerToysRunnerPid}");

                RunnerHelper.WaitForPowerToysRunner(_powerToysRunnerPid, () =>
                {
                    Logger.LogInfo("PowerToys Runner exited. Exiting TextExtractor");
                    NativeThreadCTS.Cancel();
                    _dispatcherQueue?.TryEnqueue(() => Exit());
                });
                var userSettings = new UserSettings(new ThrottledActionInvoker());
                eventMonitor = new EventMonitor(NativeThreadCTS.Token);
            }
            catch (Exception ex)
            {
                Logger.LogError($"TextExtractor got an exception on start: {ex}");
            }
        }
        else
        {
            Logger.LogInfo($"TextExtractor started detached from PowerToys Runner.");
            _powerToysRunnerPid = -1;
            var userSettings = new UserSettings(new ThrottledActionInvoker());
            keyboardMonitor = new KeyboardMonitor(userSettings);
            keyboardMonitor?.Start();
        }
    }

    public static void TrackOverlay(OCROverlay overlay)
    {
        _overlays.Add(overlay);
    }

    public static void UntrackOverlay(OCROverlay overlay)
    {
        _overlays.Remove(overlay);
    }

    public static IReadOnlyList<OCROverlay> Overlays => _overlays;

    private new void Exit()
    {
        _instanceMutex?.ReleaseMutex();
        Dispose();
        Environment.Exit(0);
    }
}
