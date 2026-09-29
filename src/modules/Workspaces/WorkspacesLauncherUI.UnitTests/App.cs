// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace WorkspacesLauncherUI
{
    // Only the App callback surface is substituted; the linked view model and warning are production code.
    internal static class App
    {
        public static Action<string> IPCMessageReceivedCallback { get; set; }

        public static void SendIPCMessage(string message)
        {
            throw new InvalidOperationException("Unit tests must inject the launcher send delegate.");
        }
    }
}
