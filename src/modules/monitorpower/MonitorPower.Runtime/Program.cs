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

    internal static int? RunnerProcessId { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        var runnerPidIndex = Array.IndexOf(args, "--runner-pid");
        if (runnerPidIndex >= 0 && runnerPidIndex + 1 < args.Length &&
            int.TryParse(args[runnerPidIndex + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var runnerProcessId))
        {
            RunnerProcessId = runnerProcessId;
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        _instanceMutex = new Mutex(initiallyOwned: true, "Local\\PowerToys_MonitorPower_Runtime", out var createdNew);
        if (!createdNew)
        {
            return 0;
        }

        Application.Start(_ =>
        {
            _application = new App();
        });
        return 0;
    }
}
