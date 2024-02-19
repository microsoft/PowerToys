// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Run.Plugin.Registry.Classes;
using Microsoft.PowerToys.Run.Plugin.Registry.Constants;
using Microsoft.PowerToys.Run.Plugin.Registry.Enumerations;
using Microsoft.PowerToys.Run.Plugin.Registry.Properties;
using Microsoft.Win32;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.Registry.Helper
{
    /// <summary>
    /// Helper class to easier work with results
    /// </summary>
    internal static class ResultHelper
    {
        /// <summary>
        /// Return a list with <see cref="Result"/>s, based on the given list
        /// </summary>
        /// <param name="list">The original result list to convert</param>
        /// <param name="iconPath">The path to the icon of each entry</param>
        /// <returns>A list with <see cref="Result"/></returns>
        internal static List<Result> GetResultList(in IEnumerable<RegistryEntry> list, in string iconPath)
        {
            var resultList = new List<Result>();

            foreach (var entry in list)
            {
                var result = new Result
                {
                    IcoPath = iconPath,
                };

                if (entry.Exception is null && !(entry.Key is null))
                {
                    // when key contains keys or fields
                    result.QueryTextDisplay = entry.Key.Name;
                    result.SubTitle = RegistryHelper.GetSummary(entry.Key);
                    result.Title = GetTruncatedText(entry.Key.Name, MaxTextLength.MaximumTitleLengthWithTwoSymbols);
                }
                else if (entry.Key is null && !(entry.Exception is null))
                {
                    // on error (e.g access denied)
                    result.QueryTextDisplay = entry.KeyPath;
                    result.SubTitle = GetTruncatedText(entry.Exception.Message, MaxTextLength.MaximumSubTitleLengthWithTwoSymbols, TruncateSide.OnlyFromRight);
                    result.Title = GetTruncatedText(entry.KeyPath, MaxTextLength.MaximumTitleLengthWithTwoSymbols);
                }
                else
                {
                    result.QueryTextDisplay = entry.KeyPath;
                    result.Title = GetTruncatedText(entry.KeyPath, MaxTextLength.MaximumTitleLengthWithTwoSymbols);
                }

                result.Action = (_) => ContextMenuHelper.TryToOpenInRegistryEditor(entry);
                result.ContextData = entry;
                result.ToolTipData = new ToolTipData(Resources.RegistryKey, $"{Resources.KeyName} {result.Title}");

                resultList.Add(result);
            }

            return resultList;
        }

        /// <summary>
        /// Return a list with <see cref="Result"/>s, based on the given <see cref="RegistryKey"/>
        /// </summary>
        /// <param name="key">The <see cref="RegistryKey"/> that should contain entries for the list</param>
        /// <param name="iconPath">The path to the icon of each entry</param>
        /// <param name="searchValue">(optional) When not <see cref="string.Empty"/> filter the list for the given value name and value</param>
        /// <returns>A list with <see cref="Result"/></returns>
        internal static List<Result> GetValuesFromKey(in RegistryKey? key, in string iconPath, string searchValue = "")
        {
            if (key is null)
            {
                return new List<Result>(0);
            }

            List<KeyValuePair<string, object>> valueList = new List<KeyValuePair<string, object>>(key.ValueCount);

            var resultList = new List<Result>();

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

                    resultList.Add(new Result
                    {
                        ContextData = registryEntry,
                        IcoPath = iconPath,
                        SubTitle = GetTruncatedText(valueException.Message, MaxTextLength.MaximumSubTitleLengthWithThreeSymbols, TruncateSide.OnlyFromRight),
                        Title = GetTruncatedText(key.Name, MaxTextLength.MaximumTitleLengthWithThreeSymbols),
                        ToolTipData = new ToolTipData(valueException.Message, valueException.ToString()),
                        Action = (_) => ContextMenuHelper.TryToOpenInRegistryEditor(registryEntry),
                        QueryTextDisplay = key.Name,
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

                    resultList.Add(new Result
                    {
                        ContextData = registryEntry,
                        IcoPath = iconPath,
                        SubTitle = GetTruncatedText(GetSubTileForRegistryValue(key, valueEntry), MaxTextLength.MaximumSubTitleLengthWithThreeSymbols, TruncateSide.OnlyFromRight),
                        Title = GetTruncatedText(valueName, MaxTextLength.MaximumTitleLengthWithThreeSymbols),
                        ToolTipData = new ToolTipData(Resources.RegistryValue, GetToolTipTextForRegistryValue(key, valueEntry)),
                        Action = (_) => ContextMenuHelper.TryToOpenInRegistryEditor(registryEntry),

                        // Avoid user handling interrupt when move up/down inside the results of a registry key
                        QueryTextDisplay = $"{key.Name}{QueryHelper.QuerySplitCharacter}",
                    });
                }
            }
            catch (Exception exception)
            {
                var registryEntry = new RegistryEntry(key.Name, exception);

                resultList.Add(new Result
                {
                    ContextData = registryEntry,
                    IcoPath = iconPath,
                    SubTitle = GetTruncatedText(exception.Message, MaxTextLength.MaximumSubTitleLengthWithThreeSymbols, TruncateSide.OnlyFromRight),
                    Title = GetTruncatedText(key.Name, MaxTextLength.MaximumTitleLengthWithThreeSymbols),
                    ToolTipData = new ToolTipData(exception.Message, exception.ToString()),
                    Action = (_) => ContextMenuHelper.TryToOpenInRegistryEditor(registryEntry),
                    QueryTextDisplay = key.Name,
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
}
