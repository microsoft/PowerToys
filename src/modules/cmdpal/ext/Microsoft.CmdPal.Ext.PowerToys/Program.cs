// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using ManagedCommon;
using Microsoft.CmdPal.Ext.PowerToys.ComServer;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.PowerToys;

public static class Program
{
    [MTAThread]
    public static int Main(string[] args)
    {
        try
        {
            Logger.InitializeLogger("\\CmdPal\\PowerToysExtension\\Logs");
        }
        catch
        {
            // If logging fails we still continue; CmdPal host will surface failures.
        }

        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "unknown";
        Logger.LogInfo($"PowerToys CmdPal extension entry point. exe={exePath}, args=\"{string.Join(' ', args)}\"");

        if (args.Length > 0 && args[0].Equals("-RegisterProcessAsComServer", StringComparison.OrdinalIgnoreCase))
        {
            using PowerToysExtensionServer server = new();
            using ManualResetEvent extensionDisposed = new(false);
            var extensionInstance = new PowerToysExtension(extensionDisposed);

            server.RegisterExtension(() => extensionInstance);
            Logger.LogInfo("Registered PowerToys CmdPal extension COM server. Waiting for dispose signal.");
            extensionDisposed.WaitOne();
            return 0;
        }

        Logger.LogWarning("PowerToys CmdPal extension launched without COM registration arguments. Exiting.");
        Console.WriteLine("Microsoft.CmdPal.Ext.PowerToys launched without COM registration arguments. Exiting.");
        return 0;
    }
}
