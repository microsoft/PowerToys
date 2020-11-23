// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
        /// <returns>A list with <see cref="Result"/></returns>
        internal static List<Result> GetResultList(ICollection<RegistryEntry> list, string iconPath)
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
                    result.Title = entry.Key.Name;
                    result.SubTitle = RegistryHelper.GetSummary(entry.Key);
                }
                else if (entry.Key is null && !(entry.Exception is null))
                {
                    // on error (e.g access denied)
                    result.Title = entry.KeyPath;
                    result.SubTitle = entry.Exception.Message;
                }
                else
                {
                    result.Title = entry.KeyPath;
                }

                result.ToolTipData = new ToolTipData("Registry key", $"Key:\t{result.Title}");
                result.ContextData = entry;

                resultList.Add(result);
            }

            return resultList;
        }

        /// <summary>
        /// Return a list with <see cref="Result"/>s, based on the given <see cref="RegistryKey"/>
        /// </summary>
        /// <param name="key">The <see cref="RegistryKey"/> that should contain entries for the list</param>
        /// <param name="iconPath">The path to the icon of each entry</param>
        /// <returns>A list with <see cref="Result"/></returns>
        internal static List<Result> GetValuesFromKey(RegistryKey? key, string iconPath)
        {
            if (key is null)
            {
                return new List<Result>(0);
            }

            var resultList = new List<Result>();
            try
            {
                foreach (var name in key.GetValueNames())
                {
                    try
                    {
                        resultList.Add(new Result
                        {
                            ContextData = new RegistryEntry(key),
                            IcoPath = iconPath,
                            SubTitle = $"Type: {ValueHelper.GetType(key, name)} * Value: {ValueHelper.GetValue(key, name, 50)}",
                            Title = name,
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
                            Title = name,
                            ToolTipData = new ToolTipData(exception.Message, exception.ToString()),
                            QueryTextDisplay = key.Name,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                resultList.Add(new Result
                {
                    ContextData = new RegistryEntry(key.Name, ex),
                    IcoPath = iconPath,
                    SubTitle = ex.Message,
                    Title = key.Name,
                    ToolTipData = new ToolTipData(ex.Message, ex.ToString()),
                    QueryTextDisplay = key.Name,
                });
            }

            return resultList;
        }

        #pragma warning restore CA1031 // Do not catch general exception types
    }
}
