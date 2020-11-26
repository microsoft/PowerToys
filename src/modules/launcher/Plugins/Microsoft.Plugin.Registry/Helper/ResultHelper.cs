// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Plugin.Registry.Classes;
using Microsoft.Win32;
using Wox.Plugin;

namespace Microsoft.Plugin.Registry.Helper
{
    /// <summary>
    /// Helper class to easier work with results
    /// </summary>
    internal static class ResultHelper
    {
        #pragma warning disable CA1031 // Do not catch general exception types

        /// <summary>
        /// Return a list with <see cref="Result"/>s, based on the given list
        /// </summary>
        /// <param name="list">The original result list to convert</param>
        /// <param name="iconPath">The path to the icon of each entry</param>
        /// <param name="maxLength">(optional) The maximum length of result text</param>
        /// <returns>A list with <see cref="Result"/></returns>
        internal static List<Result> GetResultList(in ICollection<RegistryEntry> list, in string iconPath, in int maxLength = 45)
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
                    result.Title = GetTruncatedText(entry.Key.Name, maxLength);
                }
                else if (entry.Key is null && !(entry.Exception is null))
                {
                    // on error (e.g access denied)
                    result.QueryTextDisplay = entry.KeyPath;
                    result.SubTitle = entry.Exception.Message;
                    result.Title = GetTruncatedText(entry.KeyPath, maxLength);
                }
                else
                {
                    result.QueryTextDisplay = entry.KeyPath;
                    result.Title = GetTruncatedText(entry.KeyPath, maxLength);
                }

                result.ContextData = entry;
                result.ToolTipData = new ToolTipData("Registry key", $"Key:\t{result.Title}");

                resultList.Add(result);
            }

            return resultList;
        }

        /// <summary>
        /// Return a list with <see cref="Result"/>s, based on the given <see cref="RegistryKey"/>
        /// </summary>
        /// <param name="key">The <see cref="RegistryKey"/> that should contain entries for the list</param>
        /// <param name="iconPath">The path to the icon of each entry</param>
        /// <param name="valueName">(optional) When not <see cref="string.Empty"/> filter the list for the given value name</param>
        /// <param name="maxLength">(optional) The maximum length of result text</param>
        /// <returns>A list with <see cref="Result"/></returns>
        internal static List<Result> GetValuesFromKey(in RegistryKey? key, in string iconPath, string valueName = "", in int maxLength = 45)
        {
            if (key is null)
            {
                return new List<Result>(0);
            }

            IEnumerable<string> valueNameList;

            try
            {
                valueNameList = key.GetValueNames().AsEnumerable();
            }
            catch (Exception ex)
            {
                return new List<Result>(1)
                {
                    new Result
                    {
                        ContextData = new RegistryEntry(key.Name, ex),
                        IcoPath = iconPath,
                        SubTitle = ex.Message,
                        Title = GetTruncatedText(key.Name, maxLength),
                        ToolTipData = new ToolTipData(ex.Message, ex.ToString()),
                        QueryTextDisplay = key.Name,
                    },
                };
            }

            var resultList = new List<Result>();

            if (!string.IsNullOrEmpty(valueName))
            {
                valueNameList = valueNameList.Where(found => found.Contains(valueName, StringComparison.InvariantCultureIgnoreCase));
            }

            foreach (var name in valueNameList)
            {
                try
                {
                    resultList.Add(new Result
                    {
                        ContextData = new RegistryEntry(key),
                        IcoPath = iconPath,
                        SubTitle = $"Type: {ValueHelper.GetType(key, name)} * Value: {ValueHelper.GetValue(key, name, 50)}",
                        Title = GetTruncatedText(name, maxLength),
                        ToolTipData = new ToolTipData("Registry value", $"Key:\t{key.Name}\nName:\t{name}\nType:\t{ValueHelper.GetType(key, name)}\nValue:\t{ValueHelper.GetValue(key, name)}"),
                        QueryTextDisplay = key.Name,
                    });
                }
                catch (Exception exception)
                {
                    resultList.Add(new Result
                    {
                        ContextData = new RegistryEntry(key.Name, exception),
                        IcoPath = iconPath,
                        SubTitle = exception.Message,
                        Title = GetTruncatedText(name, maxLength),
                        ToolTipData = new ToolTipData(exception.Message, exception.ToString()),
                        QueryTextDisplay = key.Name,
                    });
                }
            }

            return resultList;
        }

#pragma warning restore CA1031 // Do not catch general exception types

        /// <summary>
        /// Return a truncated name (right based with three left dots)
        /// </summary>
        /// <param name="text">The text to truncate</param>
        /// <param name="maxLength">(optional) The maximum length of the text</param>
        /// <returns>A truncated text with a maximum length</returns>
        internal static string GetTruncatedText(string text, in int maxLength = 45)
        {
            if (text.Length > maxLength)
            {
                text = QueryHelper.GetKeyWithShortBaseKey(text);
            }

            return text.Length > maxLength ? "..." + text[^maxLength..] : text;
        }
    }
}
