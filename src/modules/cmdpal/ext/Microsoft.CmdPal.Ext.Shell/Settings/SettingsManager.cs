// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell.Helpers;

public class SettingsManager : JsonSettingsManager, ISettingsInterface
{
    private static readonly string _namespace = "shell";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
    };

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

        return Path.Combine(directory, $"{_namespace}.settings.json");
    }

    private static string LegacySettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        return Path.Combine(directory, "settings.json");
    }

    /// <summary>
    /// Migrates settings from a shared legacy file to this extension's own settings file.
    /// Call after registering all settings with <see cref="Settings"/> and before <see cref="LoadSettings"/>.
    /// Skips if <see cref="FilePath"/> already exists or <paramref name="legacyFilePath"/> is missing.
    /// </summary>
    private void MigrateFromLegacyFile(string legacyFilePath)
    {
        if (string.IsNullOrEmpty(FilePath) || string.IsNullOrEmpty(legacyFilePath))
        {
            return;
        }

        // Already migrated — per-extension file exists.
        if (File.Exists(FilePath))
        {
            return;
        }

        if (!File.Exists(legacyFilePath))
        {
            return;
        }

        try
        {
            var legacyContent = File.ReadAllText(legacyFilePath);
            if (JsonNode.Parse(legacyContent) is not JsonObject)
            {
                return;
            }

            // Extract only the keys this extension owns.
            Settings.Update(legacyContent);
            var settingsJson = Settings.ToJson();

            if (JsonNode.Parse(settingsJson) is JsonObject extracted && extracted.Count > 0)
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(FilePath, extracted.ToJsonString(_serializerOptions));
            }
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"Settings migration failed from '{legacyFilePath}' to '{FilePath}': {ex}" });
        }
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_leaveShellOpen);
        Settings.Add(_shellCommandExecution);

        MigrateFromLegacyFile(LegacySettingsJsonPath());
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
