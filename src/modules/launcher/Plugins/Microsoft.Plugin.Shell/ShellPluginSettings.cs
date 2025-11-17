// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace Microsoft.Plugin.Shell
{
    public class ShellPluginSettings
    {
        public ExecutionShell Shell { get; set; } = ExecutionShell.RunCommand;

        // not overriding Win+R
        // crutkas we need to earn the right for Win+R override
        public bool ReplaceWinR { get; set; }

        public bool LeaveShellOpen { get; set; }

        public bool RunAsAdministrator { get; set; }

        public Dictionary<string, int> Count { get; } = new Dictionary<string, int>();

        public void AddCmdHistory(string cmdName)
        {
            if (Count.TryGetValue(cmdName, out int currentCount))
            {
                Count[cmdName] = currentCount + 1;
            }
            else
            {
                Count[cmdName] = 1;
            }
        }
    }

    public enum ExecutionShell
    {
        Cmd = 0,
        Powershell = 1,
        RunCommand = 2,
        WindowsTerminalPowerShell = 3,
        WindowsTerminalPowerShellSeven = 4,
        WindowsTerminalCmd = 5,
        PowerShellSeven = 6,
    }
}
