// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.PowerToys;

public static class Program
{
    [MTAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("-RegisterProcessAsComServer", StringComparison.OrdinalIgnoreCase))
        {
            using ExtensionServer server = new();
            using ManualResetEvent extensionDisposed = new(false);
            var extensionInstance = new PowerToysExtension(extensionDisposed);

            server.RegisterExtension(() => extensionInstance);

            extensionDisposed.WaitOne();
            return 0;
        }

        Console.WriteLine("Microsoft.CmdPal.Ext.PowerToys launched without COM registration arguments. Exiting.");
        return 0;
    }
}
