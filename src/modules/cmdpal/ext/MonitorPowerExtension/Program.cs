// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions;

namespace MonitorPowerExtension;

public class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
        {
            using ExtensionServer server = new();
            var extensionDisposedEvent = new ManualResetEvent(false);
            var extensionInstance = new MonitorPowerExtension(extensionDisposedEvent);
            server.RegisterExtension(() => extensionInstance, restrictToMicrosoftExtensionHosts: true);
            extensionDisposedEvent.WaitOne();
        }
    }
}
