// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.WebSearch.Commands;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers;

public class SettingsManager
{
    private readonly string _filePath;
    private readonly string _historyPath;
    private readonly Microsoft.CmdPal.Extensions.Helpers.Settings _settings = new();

    private readonly List<ChoiceSetSetting.Choice> _choices = new()
    {
        new ChoiceSetSetting.Choice(Resources.history_none, Resources.history_none),
        new ChoiceSetSetting.Choice(Resources.history_1, Resources.history_1),
        new ChoiceSetSetting.Choice(Resources.history_5, Resources.history_5),
        new ChoiceSetSetting.Choice(Resources.history_10, Resources.history_10),
        new ChoiceSetSetting.Choice(Resources.history_20, Resources.history_20),
    };

    private readonly ToggleSetting _globalIfURI = new(nameof(GlobalIfURI), Resources.plugin_global_if_uri, Resources.plugin_global_if_uri, false);
    private readonly ChoiceSetSetting _showHistory;

    public bool GlobalIfURI => _globalIfURI.Value;

    public string ShowHistory => _showHistory.Value != null ? _showHistory.Value : string.Empty;

    internal static string SettingsJsonPath()
    {
        // Get the path to our exe
        var path = System.Reflection.Assembly.GetExecutingAssembly().Location;

        // Get the directory of the exe
        var directory = Path.GetDirectoryName(path) ?? string.Empty;

        // now, the state is just next to the exe
        return Path.Combine(directory, "websearch_state.json");
    }

    internal static string HistoryStateJsonPath()
    {
        // Get the path to our exe
        var path = System.Reflection.Assembly.GetExecutingAssembly().Location;

        // Get the directory of the exe
        var directory = Path.GetDirectoryName(path) ?? string.Empty;

        // now, the state is just next to the exe
        return Path.Combine(directory, "websearch_history.json");
    }

    public void SaveHistory(HistoryItem historyItem)
    {
        if (historyItem == null)
        {
            return;
        }

        try
        {
            List<HistoryItem> historyItems;

            // Check if the file exists and load existing history
            if (File.Exists(_historyPath))
            {
                var existingContent = File.ReadAllText(_historyPath);
                historyItems = JsonSerializer.Deserialize<List<HistoryItem>>(existingContent) ?? new List<HistoryItem>();
            }
            else
            {
                historyItems = new List<HistoryItem>();
            }

            // Add the new history item
            historyItems.Add(historyItem);

            // Determine the maximum number of items to keep based on ShowHistory
            if (int.TryParse(ShowHistory, out var maxHistoryItems) && maxHistoryItems > 0)
            {
                // Keep only the most recent `maxHistoryItems` items
                while (historyItems.Count > maxHistoryItems)
                {
                    historyItems.RemoveAt(0); // Remove the oldest item
                }
            }

            // Serialize the updated list back to JSON and save it
            var historyJson = JsonSerializer.Serialize(historyItems);
            File.WriteAllText(_historyPath, historyJson);
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
        }
    }

    public List<ListItem> LoadHistory()
    {
        try
        {
            if (!File.Exists(_historyPath))
            {
                return [];
            }

            // Read and deserialize JSON into a list of HistoryItem objects
            var fileContent = File.ReadAllText(_historyPath);
            var historyItems = JsonSerializer.Deserialize<List<HistoryItem>>(fileContent) ?? [];

            // Convert each HistoryItem to a ListItem
            var listItems = new List<ListItem>();
            foreach (var historyItem in historyItems)
            {
                listItems.Add(new ListItem(new SearchWebCommand(historyItem.SearchString, this))
                {
                    Title = historyItem.SearchString,
                    Subtitle = historyItem.Timestamp.ToString("g", CultureInfo.InvariantCulture), // Ensures consistent formatting
                });
            }

            listItems.Reverse();
            return listItems;
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = ex.ToString() });
            return [];
        }
    }

    public SettingsManager()
    {
        _filePath = SettingsJsonPath();
        _historyPath = HistoryStateJsonPath();
        _showHistory = new(nameof(ShowHistory), Resources.plugin_show_history, Resources.plugin_show_history, _choices);

        _settings.Add(_globalIfURI);
        _settings.Add(_showHistory);

        // Load settings from file upon initialization
        LoadSettings();
    }

    private void ClearHistory()
    {
        try
        {
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
    }

    public Microsoft.CmdPal.Extensions.Helpers.Settings GetSettings() => _settings;

    public void SaveSettings()
    {
        try
        {
            // Serialize the main dictionary to JSON and save it to the file
            var settingsJson = _settings.ToJson();

            File.WriteAllText(_filePath, settingsJson);

            if (ShowHistory == Resources.history_none)
            {
                ClearHistory();
            }
            else if (int.TryParse(ShowHistory, out var maxHistoryItems) && maxHistoryItems > 0)
            {
                // Trim the history file if there are more items than the new limit
                if (File.Exists(_historyPath))
                {
                    var existingContent = File.ReadAllText(_historyPath);
                    var historyItems = JsonSerializer.Deserialize<List<HistoryItem>>(existingContent) ?? new List<HistoryItem>();

                    // Check if trimming is needed
                    if (historyItems.Count > maxHistoryItems)
                    {
                        // Trim the list to keep only the most recent `maxHistoryItems` items
                        historyItems = historyItems.Skip(historyItems.Count - maxHistoryItems).ToList();

                        // Save the trimmed history back to the file
                        var trimmedHistoryJson = JsonSerializer.Serialize(historyItems);
                        File.WriteAllText(_historyPath, trimmedHistoryJson);
                    }
                }
            }
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
