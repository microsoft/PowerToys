// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Shell.Helpers;

public class SettingsManager
{
    private readonly string _filePath;
    private readonly Settings _settings = new();

    private readonly List<ChoiceSetSetting.Choice> _choices = new()
    {
        new ChoiceSetSetting.Choice(Resources.find_executable_file_and_run_it, "2"), // idk why but this is how PT Run did it? Maybe ordering matters there
        new ChoiceSetSetting.Choice(Resources.run_command_in_command_prompt, "0"),
        new ChoiceSetSetting.Choice(Resources.run_command_in_powershell, "1"),
        new ChoiceSetSetting.Choice(Resources.run_command_in_powershell_seven, "6"),
        new ChoiceSetSetting.Choice(Resources.run_command_in_windows_terminal_cmd, "5"),
        new ChoiceSetSetting.Choice(Resources.run_command_in_windows_terminal_powershell, "3"),
        new ChoiceSetSetting.Choice(Resources.run_command_in_windows_terminal_powershell_seven, "4"),
    };

    private readonly ToggleSetting _leaveShellOpen = new(nameof(LeaveShellOpen), Resources.leave_shell_open, Resources.leave_shell_open,  false); // TODO -- double check default value
    private readonly ChoiceSetSetting _shellCommandExecution;

    public bool LeaveShellOpen => _leaveShellOpen.Value;

    public string ShellCommandExecution => _shellCommandExecution.Value != null ? _shellCommandExecution.Value : string.Empty;

    public bool RunAsAdministrator { get; set; }

    public Dictionary<string, int> Count { get; } = new Dictionary<string, int>();

    public void AddCmdHistory(string cmdName)
    {
        if (Count.TryGetValue(cmdName, out var currentCount))
        {
            Count[cmdName] = currentCount + 1;
        }
        else
        {
            Count[cmdName] = 1;
        }
    }

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    internal static string SettingsJsonPath()
    {
        // Get the path to our exe
        var path = System.Reflection.Assembly.GetExecutingAssembly().Location;

        // Get the directory of the exe
        var directory = Path.GetDirectoryName(path) ?? string.Empty;

        // now, the state is just next to the exe
        return Path.Combine(directory, "shell-state.json");
    }

    public SettingsManager()
    {
        _filePath = SettingsJsonPath();

        _shellCommandExecution = new(nameof(ShellCommandExecution), Resources.shell_command_execution, Resources.shell_command_execution_description, _choices);

        _settings.Add(_leaveShellOpen);
        _settings.Add(_shellCommandExecution);

        // Load settings from file upon initialization
        LoadSettings();
    }

    public Settings GetSettings()
    {
        return _settings;
    }

    public void SaveSettings()
    {
        try
        {
            // Serialize the main dictionary to JSON and save it to the file
            var settingsJson = _settings.ToJson();

            File.WriteAllText(_filePath, settingsJson);
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
        }
    }

    public void LoadSettings()
    {
        if (!File.Exists(_filePath))
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = "The provided settings file does not exist" });
            return;
        }

        try
        {
            // Read the JSON content from the file
            var jsonContent = File.ReadAllText(_filePath);

            // Is it valid JSON?
            if (JsonNode.Parse(jsonContent) is JsonObject savedSettings)
            {
                _settings.Update(jsonContent);
            }
            else
            {
                ExtensionHost.LogMessage(new LogMessage() { Message = "Failed to parse settings file as JsonObject." });
            }
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
        }
    }
}
