// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Threading;

using Common.UI;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using MouseJumpUI.Helpers;
using PowerToys.Interop;

namespace MouseJumpUI;

internal static class Program
{
    private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        Logger.InitializeLogger("\\MouseJump\\Logs");
        ETWTrace etwTrace = new ETWTrace();

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredMouseJumpEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
        {
            // TODO : Log message
            Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
            return;
        }

        if (Application.HighDpiMode != HighDpiMode.PerMonitorV2)
        {
            Logger.LogError("High dpi mode is not set to PerMonitorV2.");
            return;
        }

        // validate command line arguments - we're expecting
        // a single argument containing the runner pid
        if ((args.Length != 1) || !int.TryParse(args[0], out var runnerPid))
        {
            var message = string.Join("\r\n", new[]
            {
                "Invalid command line arguments.",
                "Expected usage is:",
                string.Empty,
                $"{Assembly.GetExecutingAssembly().GetName().Name} <RunnerPid>",
            });
            Logger.LogInfo(message);
            throw new InvalidOperationException(message);
        }

        Logger.LogInfo($"Mouse Jump started from the PowerToys Runner. Runner pid={runnerPid}");

        RunnerHelper.WaitForPowerToysRunner(runnerPid, () =>
        {
            Logger.LogInfo("PowerToys Runner exited.");
            TerminateApp();
        });

        var settingsHelper = new SettingsHelper();
        var mainForm = new MainForm(settingsHelper);

        NativeEventWaiter.WaitForEventLoop(
            Constants.MouseJumpShowPreviewEvent(),
            mainForm.ShowPreview,
            Dispatcher.CurrentDispatcher,
            cancellationTokenSource.Token);

        NativeEventWaiter.WaitForEventLoop(
            Constants.TerminateMouseJumpSharedEvent(),
            TerminateApp,
            Dispatcher.CurrentDispatcher,
            cancellationTokenSource.Token);

        Application.Run();
        etwTrace?.Dispose();
    }

    private static void TerminateApp()
    {
        Logger.LogInfo("Exiting Mouse Jump.");
        cancellationTokenSource.Cancel();
        Application.Exit();
    }
}
