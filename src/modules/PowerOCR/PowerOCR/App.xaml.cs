// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using Common.UI;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using PowerOCR.Keyboard;
using PowerOCR.Settings;
using PowerToys.Interop;

namespace PowerOCR;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application, IDisposable
{
    private KeyboardMonitor? keyboardMonitor;
    private EventMonitor? eventMonitor;
    private Mutex? _instanceMutex;
    private int _powerToysRunnerPid;
    private ETWTrace etwTrace = new ETWTrace();
    private const string ActivateOnStartupArgument = "--activate";
    private const string ExitAfterCloseArgument = "--exit-after-close";

    internal static bool ExitAfterClose { get; private set; }

    private CancellationTokenSource NativeThreadCTS { get; set; }

    public App()
    {
        Logger.InitializeLogger("\\TextExtractor\\Logs");

        try
        {
            string appLanguage = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(appLanguage))
            {
                System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(appLanguage);
            }
        }
        catch (CultureNotFoundException ex)
        {
            Logger.LogError("CultureNotFoundException: " + ex.Message);
        }

        NativeThreadCTS = new CancellationTokenSource();

        NativeEventWaiter.WaitForEventLoop(
            Constants.TerminatePowerOCRSharedEvent(),
            RequestShutdown,
            this.Dispatcher,
            NativeThreadCTS.Token);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        NativeThreadCTS.Cancel();
        NativeThreadCTS.Dispose();
        keyboardMonitor?.Dispose();
        etwTrace?.Dispose();
    }

    internal void RequestShutdown()
    {
        NativeThreadCTS.Cancel();
        if (Dispatcher.CheckAccess())
        {
            Shutdown();
        }
        else
        {
            Dispatcher.BeginInvoke(new Action(Shutdown));
        }
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredTextExtractorEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
        {
            Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
            RequestShutdown();
            return;
        }

        // allow only one instance of PowerOCR
        _instanceMutex = new Mutex(true, @"Local\PowerToys_PowerOCR_InstanceMutex", out bool createdNew);
        if (!createdNew)
        {
            Logger.LogWarning("Another running TextExtractor instance was detected. Exiting TextExtractor");
            _instanceMutex = null;
            RequestShutdown();
            return;
        }

        if (e.Args?.Length > 0)
        {
            try
            {
                _ = int.TryParse(e.Args[0], out _powerToysRunnerPid);
                bool activateOnStartup = Array.Exists(e.Args, arg => string.Equals(arg, ActivateOnStartupArgument, StringComparison.OrdinalIgnoreCase));
                ExitAfterClose = Array.Exists(e.Args, arg => string.Equals(arg, ExitAfterCloseArgument, StringComparison.OrdinalIgnoreCase));

                RunnerHelper.WaitForPowerToysRunner(_powerToysRunnerPid, () =>
                {
                    Logger.LogInfo("PowerToys Runner exited. Exiting TextExtractor");
                    RequestShutdown();
                });
                var userSettings = new UserSettings(new Helpers.ThrottledActionInvoker());
                eventMonitor = new EventMonitor(Current.Dispatcher, NativeThreadCTS.Token);
                if (activateOnStartup)
                {
                    Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            eventMonitor.StartOCRSession();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"TextExtractor startup activation failed: {ex}");
                            if (ExitAfterClose)
                            {
                                RequestShutdown();
                            }
                        }
                    }));
                }
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
            var userSettings = new UserSettings(new Helpers.ThrottledActionInvoker());
            keyboardMonitor = new KeyboardMonitor(userSettings);
            keyboardMonitor?.Start();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        NativeThreadCTS.Cancel();
        _instanceMutex?.ReleaseMutex();
        base.OnExit(e);
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        Dispose();
    }
}
