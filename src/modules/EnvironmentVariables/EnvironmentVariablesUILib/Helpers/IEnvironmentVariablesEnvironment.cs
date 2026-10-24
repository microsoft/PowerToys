// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable annotations

using System;

using Microsoft.Win32;

namespace EnvironmentVariablesUILib.Helpers
{
    internal interface IEnvironmentVariablesEnvironment
    {
        IEnvironmentVariablesRegistryKey? OpenKey(RegistryHive hive, string subKey, bool writable);

        string ExpandEnvironmentVariables(string value);

        int SendNotifyMessage(IntPtr window, int message, IntPtr wParam, string lParam);
    }
}
