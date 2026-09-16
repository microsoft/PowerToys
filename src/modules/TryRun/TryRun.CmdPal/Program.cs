// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;

namespace PowerToys.TryRun.CmdPal;

internal static class Program
{
    [MTAThread]
    public static int Main(string[] args)
    {
        if (args is ["--probe-registration", var correlation])
        {
            return RegistrationProbe.Run(correlation);
        }

        if (args is ["--probe-launch", var launchCorrelation])
        {
            return RegistrationProbe.Run(launchCorrelation, launch: true);
        }

        if (args is not ["-RegisterProcessAsComServer", ..])
        {
            return 0;
        }

        var server = new ComServer();
        using var disposed = new ManualResetEvent(false);
        using var extension = new TryRunExtension(disposed);
        try
        {
            server.RegisterClass<TryRunExtension, IExtension>(() => extension);
            server.Start();
            disposed.WaitOne();
        }
        finally
        {
            server.Stop();
            server.UnsafeDispose();
        }

        return 0;
    }
}
