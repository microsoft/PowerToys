// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Reflection;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using MouseJump.Common.Helpers;
using PowerToys.GPOWrapper;

namespace MouseJump.WinUI3;

public static class Program
{
    private static App? _app;

    [STAThread]
    public static void Main(string[] args)
    {
        Logger.InitializeLogger("\\MouseJump\\Logs");
        Logger.LogInfo("MouseJump process started");

        using var etwTrace = new ETWTrace();

        WinRT.ComWrappersSupport.InitializeComWrappers();

        // check Group Policy allows this app to run
        if (GPOWrapper.GetConfiguredMouseJumpEnabledValue() == GpoRuleConfigured.Disabled)
        {
            Logger.LogWarning(
                "Tried to start with a GPO policy setting the utility to always be disabled. " +
                "Please contact your systems administrator.");
            return;
        }

        // make sure we're in the right high dpi mode otherwise pixel positions and sizes for
        // screen captures get distorted and various coordinates aren't calculated correctly.
        Logger.LogInfo("checking high dpi mode");
        DpiModeHelper.EnsurePerMonitorV2Enabled();
        Logger.LogInfo("high dpi mode is ok");

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

        Logger.LogDebug($"Runner PID = {runnerPid}", runnerPid.ToString(CultureInfo.InvariantCulture));

        // quit the app when the parent process terminates
        RunnerHelper.WaitForPowerToysRunner(runnerPid, () =>
        {
            Logger.LogInfo("PowerToys Runner exited.");
            _app?.TerminateApp();
        });
        Logger.LogInfo($"Mouse Jump started from the PowerToys Runner. Runner pid={runnerPid}");

        // prevent multiple instances of the app from running
        var instanceKey = AppInstance.FindOrRegisterForKey("MouseJump_Instance");
        if (instanceKey.IsCurrent)
        {
            Logger.LogWarning("starting application");
            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _app = new App();
            });
            Logger.LogWarning("application exited");
        }
        else
        {
            Logger.LogWarning("another instance is running. exiting");
        }

        return;
    }
}
