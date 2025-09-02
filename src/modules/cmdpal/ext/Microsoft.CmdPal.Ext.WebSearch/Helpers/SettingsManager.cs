// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ManagedCommon;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers;

public class SettingsManager : JsonSettingsManager, ISettingsInterface
{
    private const string HistoryItemCountLegacySettingsKey = "ShowHistory";
    private static readonly string _namespace = "websearch";

    public event EventHandler? HistoryChanged;

    private readonly string _historyPath;
    private readonly List<HistoryItem> _historyItems = [];

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

    private readonly ChoiceSetSetting _historyItemCount = new(
        Namespaced(HistoryItemCountLegacySettingsKey),
        Resources.plugin_history_item_count,
        Resources.plugin_history_item_count,
        _choices);

    public bool GlobalIfURI => _globalIfURI.Value;

    public uint HistoryItemCount => uint.TryParse(_historyItemCount.Value, out var value) ? value : 0;

    public IReadOnlyList<HistoryItem> HistoryItems => _historyItems;

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();
        _historyPath = HistoryStateJsonPath();

        Settings.Add(_globalIfURI);
        Settings.Add(_historyItemCount);

        // Load settings from file upon initialization
        LoadSettings();

        _historyItems.AddRange(LoadHistoryFromDisk());

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
        ArgumentNullException.ThrowIfNull(historyItem);

        try
        {
            _historyItems.Add(historyItem);
            TrimHistory();
            SaveHistory();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to add item to the search history", ex);
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool TrimHistory()
    {
        var max = HistoryItemCount;

        if (_historyItems.Count > max)
        {
            _historyItems.RemoveRange(0, (int)(_historyItems.Count - max));
            return true;
        }

        return false;
    }

    private List<HistoryItem> LoadHistoryFromDisk()
    {
        try
        {
            if (!File.Exists(_historyPath))
            {
                return [];
            }

            // Read and deserialize JSON into a list of HistoryItem objects
            var fileContent = File.ReadAllText(_historyPath);
            var historyItems = JsonSerializer.Deserialize<List<HistoryItem>>(fileContent, WebSearchJsonSerializationContext.Default.ListHistoryItem) ?? [];

            return historyItems;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load the search history", ex);
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
            return [];
        }
    }

    public override void SaveSettings()
    {
        base.SaveSettings();

        try
        {
            var trimmed = TrimHistory();
            if (trimmed)
            {
                SaveHistory();
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to save the search history", ex);
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
        }
    }

    private void SaveHistory()
    {
        var historyJson = JsonSerializer.Serialize(_historyItems, WebSearchJsonSerializationContext.Default.ListHistoryItem);
        File.WriteAllText(_historyPath, historyJson);
    }
}
