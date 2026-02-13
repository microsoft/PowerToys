// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using ManagedCommon;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers;

public class SettingsManager : JsonSettingsManager, ISettingsInterface
{
    private const string HistoryItemCountLegacySettingsKey = "ShowHistory";
    private static readonly string _namespace = "websearch";

    public event EventHandler? HistoryChanged
    {
        add => _history.Changed += value;
        remove => _history.Changed -= value;
    }

    private readonly HistoryStore _history;

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private static readonly List<ChoiceSetSetting.Choice> _choices =
    [
        new ChoiceSetSetting.Choice(Resources.history_none, "None"),
        new ChoiceSetSetting.Choice(Resources.history_1, "1"),
        new ChoiceSetSetting.Choice(Resources.history_5, "5"),
        new ChoiceSetSetting.Choice(Resources.history_10, "10"),
        new ChoiceSetSetting.Choice(Resources.history_20, "20"),
    ];

    private readonly ToggleSetting _globalIfURI = new(
        Namespaced(nameof(GlobalIfURI)),
        Resources.plugin_global_if_uri,
        Resources.plugin_global_if_uri,
        false);

    private readonly TextSetting _customSearchUri = new(
        Namespaced(nameof(CustomSearchUri)),
        Resources.plugin_custom_search_uri,
        Resources.plugin_custom_search_uri,
        string.Empty)
    {
        Placeholder = Resources.plugin_custom_search_uri_placeholder,
    };

    private readonly ChoiceSetSetting _historyItemCount = new(
        Namespaced(HistoryItemCountLegacySettingsKey),
        Resources.plugin_history_item_count,
        Resources.plugin_history_item_count,
        _choices);

    public bool GlobalIfURI => _globalIfURI.Value;

    public int HistoryItemCount => int.TryParse(_historyItemCount.Value, out var value) && value >= 0 ? value : 0;

    public string CustomSearchUri => _customSearchUri.Value ?? string.Empty;

    public IReadOnlyList<HistoryItem> HistoryItems => _history.HistoryItems;

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_globalIfURI);
        Settings.Add(_historyItemCount);
        Settings.Add(_customSearchUri);

        LoadSettings();

        // Initialize history store after loading settings to get the correct capacity
        _history = new HistoryStore(HistoryStateJsonPath(), HistoryItemCount);

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return Path.Combine(directory, "settings.json");
    }

    private static string HistoryStateJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return Path.Combine(directory, "websearch_history.json");
    }

    public void AddHistoryItem(HistoryItem historyItem)
    {
        try
        {
            _history.Add(historyItem);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to add item to the search history", ex);
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
        }
    }

    public override void SaveSettings()
    {
        base.SaveSettings();

        try
        {
            _history.SetCapacity(HistoryItemCount);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to save the search history", ex);
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
        }
    }
}
