// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers;

public class SettingsManager : JsonSettingsManager, ISettingsInterface
{
    public event EventHandler? HistoryChanged;

    private readonly string _historyPath;
    private readonly List<HistoryItem> _historyItems = [];

    private static readonly string _namespace = "websearch";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private static readonly List<ChoiceSetSetting.Choice> _choices =
    [
        new ChoiceSetSetting.Choice(Resources.history_none, Resources.history_none),
        new ChoiceSetSetting.Choice(Resources.history_1, Resources.history_1),
        new ChoiceSetSetting.Choice(Resources.history_5, Resources.history_5),
        new ChoiceSetSetting.Choice(Resources.history_10, Resources.history_10),
        new ChoiceSetSetting.Choice(Resources.history_20, Resources.history_20),
    ];

    private readonly ToggleSetting _globalIfURI = new(
        Namespaced(nameof(GlobalIfURI)),
        Resources.plugin_global_if_uri,
        Resources.plugin_global_if_uri,
        false);

    private readonly ChoiceSetSetting _showHistory = new(
        Namespaced(nameof(ShowHistory)),
        Resources.plugin_show_history,
        Resources.plugin_show_history,
        _choices);

    public bool GlobalIfURI => _globalIfURI.Value;

    public string ShowHistory => _showHistory.Value ?? string.Empty;

    public IReadOnlyList<HistoryItem> HistoryItems => _historyItems;

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
        if (historyItem is null)
        {
            return;
        }

        var added = false;

        try
        {
            _historyItems.Add(historyItem);

            // Determine the maximum number of items to keep based on ShowHistory
            if (int.TryParse(ShowHistory, out var maxHistoryItems) && maxHistoryItems > 0)
            {
                // Keep only the most recent `maxHistoryItems` items
                while (_historyItems.Count > maxHistoryItems)
                {
                    _historyItems.RemoveAt(0); // Remove the oldest item
                }
            }

            added = true;

            // Serialize the updated list back to JSON and save it
            var historyJson = JsonSerializer.Serialize(_historyItems, WebSearchJsonSerializationContext.Default.ListHistoryItem);
            File.WriteAllText(_historyPath, historyJson);
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
        }

        if (added)
        {
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
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
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
            return [];
        }
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();
        _historyPath = HistoryStateJsonPath();

        Settings.Add(_globalIfURI);
        Settings.Add(_showHistory);

        // Load settings from file upon initialization
        LoadSettings();

        _historyItems.Clear();
        _historyItems.AddRange(LoadHistoryFromDisk());

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }

    private void ClearHistory()
    {
        try
        {
            _historyItems.Clear();

            if (File.Exists(_historyPath))
            {
                // Delete the history file
                File.Delete(_historyPath);

                // Log that the history was successfully cleared
                ExtensionHost.LogMessage(new LogMessage() { Message = "History cleared successfully." });
            }
            else
            {
                // Log that there was no history file to delete
                ExtensionHost.LogMessage(new LogMessage() { Message = "No history file found to clear." });
            }
        }
        catch (Exception ex)
        {
            // Log any exception that occurs
            ExtensionHost.LogMessage(new LogMessage() { Message = $"Failed to clear history: {ex}" });
        }
        finally
        {
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public override void SaveSettings()
    {
        base.SaveSettings();
        try
        {
            if (ShowHistory == Resources.history_none)
            {
                ClearHistory();
            }
            else if (int.TryParse(ShowHistory, out var maxHistoryItems) && maxHistoryItems > 0)
            {
                // Trim the in-memory history if there are more items than the new limit
                if (_historyItems.Count > maxHistoryItems)
                {
                    // Trim the list to keep only the most recent `maxHistoryItems` items
                    var trimmed = _historyItems.Skip(Math.Max(0, _historyItems.Count - maxHistoryItems)).ToList();
                    _historyItems.Clear();
                    _historyItems.AddRange(trimmed);

                    // Save the trimmed history back to the file
                    var trimmedHistoryJson = JsonSerializer.Serialize(_historyItems, WebSearchJsonSerializationContext.Default.ListHistoryItem);
                    File.WriteAllText(_historyPath, trimmedHistoryJson);

                    HistoryChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
        }
    }
}
