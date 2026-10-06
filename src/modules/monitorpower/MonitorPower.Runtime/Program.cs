// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading;
using Microsoft.UI.Xaml;

namespace MonitorPower.Runtime;

public static class Program
{
    private static Mutex? _instanceMutex;
    private static App? _application;

    internal static int? OwnerProcessId { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        RuntimeLog.Info($"Runtime host process starting. Arguments: {string.Join(' ', args)}");
        var ownerPidIndex = Array.IndexOf(args, "--owner-pid");
        if (ownerPidIndex < 0)
        {
            ownerPidIndex = Array.IndexOf(args, "--runner-pid");
        }

        if (ownerPidIndex >= 0 && ownerPidIndex + 1 < args.Length &&
            int.TryParse(args[ownerPidIndex + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var ownerProcessId))
        {
            OwnerProcessId = ownerProcessId;
        }

        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            _instanceMutex = new Mutex(initiallyOwned: true, "Local\\PowerToys_MonitorPower_Runtime", out var createdNew);
            if (!createdNew)
            {
                RuntimeLog.Info("Another runtime host instance already owns the mutex; exiting.");
                return 0;
            }

            RuntimeLog.Info($"Runtime host owns the single-instance mutex. Owner PID: {OwnerProcessId?.ToString(CultureInfo.InvariantCulture) ?? "not provided"}.");
            Application.Start(_ =>
            {
                _application = new App();
            });
            RuntimeLog.Info("WinUI application loop exited.");
            return 0;
        }
        catch (Exception ex)
        {
            RuntimeLog.Error("Runtime host failed during startup or its application loop.", ex);
            throw;
        }
    }
}
