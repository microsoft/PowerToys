// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;

namespace CompatibilityExtension;

internal static class Program
{
    [MTAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--verify-fixture")
        {
            return FixtureVerification.Run();
        }

        if (args.Length != 1 || args[0] != "-RegisterProcessAsComServer")
        {
            return 0;
        }

        using var disposed = new ManualResetEvent(false);
        var extension = new Extension(disposed);
        var server = new ComServer();
        server.RegisterClass<Extension, IExtension>(() => extension);
        server.Start();
        disposed.WaitOne();
        server.Stop();
        server.UnsafeDispose();
        return 0;
    }
}
