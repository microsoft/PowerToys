// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Shell.Helpers;

namespace Microsoft.CmdPal.Ext.Shell.UnitTests;

public class Settings : ISettingsInterface
{
    private readonly bool leaveShellOpen;
    private readonly string shellCommandExecution;
    private readonly bool runAsAdministrator;
    private readonly Dictionary<string, int> count;

    public Settings(
        bool leaveShellOpen = false,
        string shellCommandExecution = "0",
        bool runAsAdministrator = false,
        Dictionary<string, int> count = null)
    {
        this.leaveShellOpen = leaveShellOpen;
        this.shellCommandExecution = shellCommandExecution;
        this.runAsAdministrator = runAsAdministrator;
        this.count = count ?? new Dictionary<string, int>();
    }

    public bool LeaveShellOpen => leaveShellOpen;

    public string ShellCommandExecution => shellCommandExecution;

    public bool RunAsAdministrator => runAsAdministrator;

    public Dictionary<string, int> Count => count;

    public void AddCmdHistory(string cmdName)
    {
        count[cmdName] = count.TryGetValue(cmdName, out var currentCount) ? currentCount + 1 : 1;
    }

    public static Settings CreateDefaultSettings() => new Settings();

    public static Settings CreateLeaveShellOpenSettings() => new Settings(leaveShellOpen: true);

    public static Settings CreatePowerShellSettings() => new Settings(shellCommandExecution: "1");

    public static Settings CreateAdministratorSettings() => new Settings(runAsAdministrator: true);
}
