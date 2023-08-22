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
            if (Count.ContainsKey(cmdName))
            {
                Count[cmdName] += 1;
            }
            else
            {
                Count.Add(cmdName, 1);
            }
        }

        public static string GetDescription(Enum value)
        {
            FieldInfo fi = value.GetType().GetField(value.ToString());

            DescriptionAttribute[] attributes =
                (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);

            if (attributes != null && attributes.Length > 0)
            {
                return attributes[0].Description;
            }
            else
            {
                return value.ToString();
            }
        }
    }

    public enum ExecutionShell
    {
        [Description("Run command in Command Prompt (cmd.exe)")]
        Cmd = 0,
        [Description("Run command in PowerShell (PowerShell.exe)")]
        Powershell = 1,
        [Description("Find executable file and run it")]
        RunCommand = 2,
        [Description("Run command in Windows Terminal (wt.exe)")]
        WindowsTerminal = 3,
    }
}
