// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using ManagedCommon;
using Microsoft.CmdPal.Ext.WindowsTerminal.Pages;
using Microsoft.CmdPal.Ext.WindowsTerminal.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

#nullable enable

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

public class SettingsManager : JsonSettingsManager
{
    private static readonly string _namespace = "wt";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private readonly ToggleSetting _showHiddenProfiles = new(
        Namespaced(nameof(ShowHiddenProfiles)),
        Resources.show_hidden_profiles,
        Resources.show_hidden_profiles,
        false);

    private readonly ToggleSetting _openNewTab = new(
        Namespaced(nameof(OpenNewTab)),
        Resources.open_new_tab,
        Resources.open_new_tab,
        false);

    private readonly ToggleSetting _openQuake = new(
        Namespaced(nameof(OpenQuake)),
        Resources.open_quake,
        Resources.open_quake_description,
        false);

    private readonly ChoiceSetSetting _preferredChannel = new(
        Namespaced(nameof(PreferredChannelAppId)),
        Resources.preferred_channel!,
        Resources.preferred_channel_description!,
        GetTerminals());

    private readonly ToggleSetting _saveLastSelectedChannel = new(
        Namespaced(nameof(SaveLastSelectedChannel)),
        Resources.save_last_selected_channel!,
        Resources.save_last_selected_channel_description!,
        false);

    public bool ShowHiddenProfiles => _showHiddenProfiles.Value;

    public bool OpenNewTab => _openNewTab.Value;

    public bool OpenQuake => _openQuake.Value;

    public bool SaveLastSelectedChannel => _saveLastSelectedChannel.Value;

    public string PreferredChannelAppId
    {
        get => _preferredChannel.Value ?? TerminalChannelFilters.AllTerminalsFilterId;
        set => _preferredChannel.Value = value;
    }

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_showHiddenProfiles);
        Settings.Add(_openNewTab);
        Settings.Add(_openQuake);
        Settings.Add(_preferredChannel);
        Settings.Add(_saveLastSelectedChannel);

        // Load profiles
        _preferredChannel.Choices = GetTerminals();

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }

    private static List<ChoiceSetSetting.Choice> GetTerminals()
    {
        List<ChoiceSetSetting.Choice> choices = [new(Resources.all_channels, TerminalChannelFilters.AllTerminalsFilterId)];

        try
        {
            var terminalQuery = new TerminalQuery();
            foreach (var terminalPackage in terminalQuery.GetTerminals())
            {
                choices.Add(new ChoiceSetSetting.Choice(terminalPackage.DisplayName, terminalPackage.AppUserModelId));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error listing Windows Terminal packages", ex);
        }

        return choices;
    }
}
