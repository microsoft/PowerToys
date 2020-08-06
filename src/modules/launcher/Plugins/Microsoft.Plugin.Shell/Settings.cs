// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Plugin.Shell
{
    public class Settings
    {
        public Shell Shell { get; set; } = Shell.RunCommand;

        // not overriding Win+R
        // crutkas we need to earn the right for Win+R override
        public bool ReplaceWinR { get; set; } = false;

        public bool LeaveShellOpen { get; set; }

        public bool RunAsAdministrator { get; set; } = false;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "StyleCop error, actually used in main.cs")]
        public Dictionary<string, int> Count = new Dictionary<string, int>();

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
    }

    public enum Shell
    {
        Cmd = 0,
        Powershell = 1,
        RunCommand = 2,
    }
}
