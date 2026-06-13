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
using Microsoft.PowerToys.Telemetry;
using MouseJumpUI.Helpers;
using MouseJumpUI.UI;
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
        try
        {
            Logger.InitializeLogger("\\MouseJump\\Logs");
            ETWTrace etwTrace = new ETWTrace();

            Logger.LogDebug("MouseJump started");

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

            Logger.LogInfo("Waiting for PowerToys runner.");
            RunnerHelper.WaitForPowerToysRunner(runnerPid, () =>
            {
                Logger.LogInfo("PowerToys Runner exited.");
                TerminateApp();
            });

            Logger.LogInfo("Creating main form.");
            var settingsHelper = new SettingsHelper();
            var mainForm = new MainForm(settingsHelper);

            // touch the form handle - this will force the handle to be created if it hasn't
            // been already (we'll get an error from previewForm.BeginInvoke() if the form
            // handle doesn't exist). note that BeginInvoke() will block whatever thread is
            // the owner of the handle so we need to make sure it gets created on the main
            // application thread otherwise we might block something important like the
            // hotkey message loop
            _ = mainForm.Handle;

            Logger.LogInfo("Starting 'show preview' event handler");
            MouseJumpEventLoop.RunEventHandler(
                Constants.MouseJumpShowPreviewEvent(),
                mainForm.ShowPreviewAsync,
                Dispatcher.CurrentDispatcher,
                cancellationTokenSource.Token);

            Logger.LogInfo("Starting 'terminate' event loop");
            MouseJumpEventLoop.RunEventHandler(
                Constants.TerminateMouseJumpSharedEvent(),
                TerminateApp,
                Dispatcher.CurrentDispatcher,
                cancellationTokenSource.Token);

            Logger.LogInfo("Starting application loop");
            Application.Run();
            etwTrace?.Dispose();

            Logger.LogInfo("MouseJump ended");
        }
        catch (Exception ex)
        {
            Logger.LogInfo(ex.ToString());
            throw;
        }
    }

    private static void TerminateApp()
    {
        Logger.LogInfo("Exiting Mouse Jump.");
        cancellationTokenSource.Cancel();
        Application.Exit();
    }
}
