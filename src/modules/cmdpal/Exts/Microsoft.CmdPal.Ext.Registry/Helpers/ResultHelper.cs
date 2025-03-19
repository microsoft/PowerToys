// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CmdPal.Ext.Registry.Classes;
using Microsoft.CmdPal.Ext.Registry.Commands;
using Microsoft.CmdPal.Ext.Registry.Constants;
using Microsoft.CmdPal.Ext.Registry.Enumerations;
using Microsoft.CmdPal.Ext.Registry.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.Registry.Helpers;

/// <summary>
/// Helper class to easier work with results
/// </summary>
internal static class ResultHelper
{
    /// <summary>
    /// Return a list with <see cref="Result"/>s, based on the given list
    /// </summary>
    /// <param name="list">The original result list to convert</param>
    /// <returns>A list with <see cref="Result"/></returns>
    internal static List<ListItem> GetResultList(in IEnumerable<RegistryEntry> list)
    {
        var resultList = new List<ListItem>();

        foreach (var entry in list)
        {
            var result = new ListItem(new OpenKeyInEditorCommand(entry))
            {
                Icon = RegistryListPage.RegistryIcon,
                MoreCommands = ContextMenuHelper.GetContextMenu(entry).ToArray(),
            };

            if (entry.Exception is null && entry.Key is not null)
            {
                // when key contains keys or fields
                result.TextToSuggest = entry.Key.Name;
                result.Subtitle = RegistryHelper.GetSummary(entry.Key);
                result.Title = GetTruncatedText(entry.Key.Name, MaxTextLength.MaximumTitleLengthWithTwoSymbols);
            }
            else if (entry.Key is null && entry.Exception is not null)
            {
                // on error (e.g access denied)
                result.TextToSuggest = entry.KeyPath;
                result.Subtitle = GetTruncatedText(entry.Exception.Message, MaxTextLength.MaximumSubTitleLengthWithTwoSymbols, TruncateSide.OnlyFromRight);
                result.Title = GetTruncatedText(entry.KeyPath, MaxTextLength.MaximumTitleLengthWithTwoSymbols);
            }
            else
            {
                result.TextToSuggest = entry.KeyPath;
                result.Title = GetTruncatedText(entry.KeyPath, MaxTextLength.MaximumTitleLengthWithTwoSymbols);
            }

            // result.ContextData = entry;
            // TODO GH #126 Investigate tool tips, result.ToolTipData = new ToolTipData(Resources.RegistryKey, $"{Resources.KeyName} {result.Title}");
            resultList.Add(result);
        }

        return resultList;
    }

#pragma warning disable CS8632
    internal static List<ListItem> GetValuesFromKey(in RegistryKey? key, string searchValue = "")
    {
#pragma warning restore CS8632
        if (key is null)
        {
            return [];
        }

        var valueList = new List<KeyValuePair<string, object>>(key.ValueCount);

        var resultList = new List<ListItem>();

        try
        {
            var valueNames = key.GetValueNames();

            try
            {
                foreach (var valueName in valueNames)
                {
                    var value = key.GetValue(valueName);
                    if (value != null)
                    {
                        valueList.Add(KeyValuePair.Create(valueName, value));
                    }
                }
            }
            catch (Exception valueException)
            {
                var registryEntry = new RegistryEntry(key.Name, valueException);

                resultList.Add(new ListItem(new OpenKeyInEditorCommand(registryEntry))
                {
                    Icon = RegistryListPage.RegistryIcon,
                    Subtitle = GetTruncatedText(valueException.Message, MaxTextLength.MaximumSubTitleLengthWithThreeSymbols, TruncateSide.OnlyFromRight),
                    Title = GetTruncatedText(key.Name, MaxTextLength.MaximumTitleLengthWithThreeSymbols),
                    MoreCommands = ContextMenuHelper.GetContextMenu(registryEntry).ToArray(),

                    // TODO --> Investigate ToolTipData = new ToolTipData(valueException.Message, valueException.ToString()),
                });
            }

            if (!string.IsNullOrEmpty(searchValue))
            {
                var filteredValueName = valueList.Where(found => found.Key.Contains(searchValue, StringComparison.InvariantCultureIgnoreCase));
                var filteredValueList = valueList.Where(found => found.Value.ToString()?.Contains(searchValue, StringComparison.InvariantCultureIgnoreCase) ?? false);

                valueList = filteredValueName.Concat(filteredValueList).Distinct().ToList();
            }

            foreach (var valueEntry in valueList.OrderBy(found => found.Key))
            {
                var valueName = valueEntry.Key;
                if (string.IsNullOrEmpty(valueName))
                {
                    valueName = "(Default)";
                }

                var registryEntry = new RegistryEntry(key, valueEntry.Key, valueEntry.Value);

                resultList.Add(new ListItem(new OpenKeyInEditorCommand(registryEntry))
                {
                    Icon = RegistryListPage.RegistryIcon,
                    Subtitle = GetTruncatedText(GetSubTileForRegistryValue(key, valueEntry), MaxTextLength.MaximumSubTitleLengthWithThreeSymbols, TruncateSide.OnlyFromRight),
                    Title = GetTruncatedText(valueName, MaxTextLength.MaximumTitleLengthWithThreeSymbols),
                    MoreCommands = ContextMenuHelper.GetContextMenu(registryEntry).ToArray(),

                    // TODO Investigate -->ToolTipData = new ToolTipData(Resources.RegistryValue, GetToolTipTextForRegistryValue(key, valueEntry)),
                });
            }
        }
        catch (Exception exception)
        {
            var registryEntry = new RegistryEntry(key.Name, exception);

            resultList.Add(new ListItem(new OpenKeyInEditorCommand(registryEntry))
            {
                Icon = RegistryListPage.RegistryIcon,
                Subtitle = GetTruncatedText(exception.Message, MaxTextLength.MaximumSubTitleLengthWithThreeSymbols, TruncateSide.OnlyFromRight),
                Title = GetTruncatedText(key.Name, MaxTextLength.MaximumTitleLengthWithThreeSymbols),
            });
        }

        return resultList;
    }

    /// <summary>
    /// Return a truncated name
    /// </summary>
    /// <param name="text">The text to truncate</param>
    /// <param name="maxLength">The maximum length of the text</param>
    /// <param name="truncateSide">(optional) The side of the truncate</param>
    /// <returns>A truncated text with a maximum length</returns>
    internal static string GetTruncatedText(string text, in int maxLength, TruncateSide truncateSide = TruncateSide.OnlyFromLeft)
    {
        if (truncateSide == TruncateSide.OnlyFromLeft)
        {
            if (text.Length > maxLength)
            {
                text = QueryHelper.GetKeyWithShortBaseKey(text);
            }

            return text.Length > maxLength ? $"...{text[^maxLength..]}" : text;
        }
        else
        {
            return text.Length > maxLength ? $"{text[0..maxLength]}..." : text;
        }
    }

    /// <summary>
    /// Return the tool-tip text for a registry value
    /// </summary>
    /// <param name="key">The registry key for the tool-tip</param>
    /// <param name="valueEntry">The value name and value of the registry value</param>
    /// <returns>A tool-tip text</returns>
    private static string GetToolTipTextForRegistryValue(RegistryKey key, KeyValuePair<string, object> valueEntry)
    {
        return $"{Resources.KeyName} {key.Name}{Environment.NewLine}"
             + $"{Resources.Name} {valueEntry.Key}{Environment.NewLine}"
             + $"{Resources.Type} {ValueHelper.GetType(key, valueEntry.Key)}{Environment.NewLine}"
             + $"{Resources.Value} {ValueHelper.GetValue(key, valueEntry.Key)}";
    }

    /// <summary>
    /// Return the sub-title text for a registry value
    /// </summary>
    /// <param name="key">The registry key for the sub-title</param>
    /// <param name="valueEntry">The value name and value of the registry value</param>
    /// <returns>A sub-title text</returns>
    private static string GetSubTileForRegistryValue(RegistryKey key, KeyValuePair<string, object> valueEntry)
    {
        return $"{Resources.Type} {ValueHelper.GetType(key, valueEntry.Key)}"
             + $" - {Resources.Value} {ValueHelper.GetValue(key, valueEntry.Key, 50)}";
    }
}
