// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell.Helpers;

public class SettingsManager : JsonSettingsManager, ISettingsInterface
{
    private static readonly string _namespace = "shell";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private static readonly List<ChoiceSetSetting.Choice> _choices =
    [
        new ChoiceSetSetting.Choice(Resources.find_executable_file_and_run_it, "2"), // idk why but this is how PT Run did it? Maybe ordering matters there
        new ChoiceSetSetting.Choice(Resources.run_command_in_command_prompt, "0"),
        new ChoiceSetSetting.Choice(Resources.run_command_in_powershell, "1"),
        new ChoiceSetSetting.Choice(Resources.run_command_in_powershell_seven, "6"),
        new ChoiceSetSetting.Choice(Resources.run_command_in_windows_terminal_cmd, "5"),
        new ChoiceSetSetting.Choice(Resources.run_command_in_windows_terminal_powershell, "3"),
        new ChoiceSetSetting.Choice(Resources.run_command_in_windows_terminal_powershell_seven, "4"),
    ];

    private readonly ToggleSetting _leaveShellOpen = new(
        Namespaced(nameof(LeaveShellOpen)),
        Resources.leave_shell_open,
        Resources.leave_shell_open,
        false); // TODO -- double check default value

    private readonly ChoiceSetSetting _shellCommandExecution = new(
        Namespaced(nameof(ShellCommandExecution)),
        Resources.shell_command_execution,
        Resources.shell_command_execution_description,
        _choices);

    public bool LeaveShellOpen => _leaveShellOpen.Value;

    public string ShellCommandExecution => _shellCommandExecution.Value ?? string.Empty;

    public bool RunAsAdministrator { get; set; }

    public Dictionary<string, int> Count { get; } = [];

    public void AddCmdHistory(string cmdName) => Count[cmdName] = Count.TryGetValue(cmdName, out var currentCount) ? currentCount + 1 : 1;

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_leaveShellOpen);
        Settings.Add(_shellCommandExecution);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
