// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;

namespace PowerToysExtension;

public class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Initialize per-extension log under CmdPal/PowerToysExtension.
            Logger.InitializeLogger("\\CmdPal\\PowerToysExtension\\Logs");
            Logger.LogInfo($"PowerToysExtension starting. Args=\"{string.Join(' ', args)}\" ProcArch={RuntimeInformation.ProcessArchitecture} OSArch={RuntimeInformation.OSArchitecture} BaseDir={AppContext.BaseDirectory}");
        }
        catch
        {
            // Continue even if logging fails.
        }

        try
        {
            if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
            {
                Logger.LogInfo("RegisterProcessAsComServer mode detected.");
                ComServer server = new();
                ManualResetEvent extensionDisposedEvent = new(false);
                try
                {
                    // AppLifeMonitor creates a hidden window on a background STA thread to handle
                    // WM_QUERYENDSESSION and WM_ENDSESSION. Without it, extensions hang on OS
                    // shutdown because the MTA main thread has no message loop to receive those messages.
                    using AppLifeMonitor monitor = new(extensionDisposedEvent);

                    PowerToysExtension extensionInstance = new(extensionDisposedEvent);
                    Logger.LogInfo("Registering extension via Shmuelie.WinRTServer.");
                    server.RegisterClass<PowerToysExtension, IExtension>(() => extensionInstance);
                    server.Start();
                    Logger.LogInfo("Extension instance registered; waiting for disposal signal.");

                    extensionDisposedEvent.WaitOne();
                    Logger.LogInfo("Extension disposed signal received; exiting server loop.");
                }
                finally
                {
                    server.Stop();
                    server.UnsafeDispose();
                }
            }
            else
            {
                Console.WriteLine("Not being launched as a Extension... exiting.");
                Logger.LogInfo("Exited: not launched with -RegisterProcessAsComServer.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Unhandled exception in PowerToysExtension.Main", ex);
            throw;
        }
        finally
        {
        }
    }
}
