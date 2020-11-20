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
    internal static class ResultHelper
    {
        #pragma warning disable CA1031 // Do not catch general exception types

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

        internal static List<Result> GetValuesFromKey(RegistryKey? key, string iconPath)
        {
            if (key is null)
            {
                return new List<Result>(0);
            }

            var resultList = new List<Result>();

            foreach (var name in key.GetValueNames())
            {
                try
                {
                    resultList.Add(new Result
                    {
                        ContextData = new RegistryEntry(key.Name, key),
                        IcoPath = iconPath,
                        SubTitle = $"Type: {ValueHelper.GetType(key, name)} * Value: {ValueHelper.GetValue(key, name, 50)}",
                        Title = name,
                        ToolTipData = new ToolTipData("Registry value", $"Key:\t{key.Name}\nName:\t{name}\nType:\t{ValueHelper.GetType(key, name)}\nValue:\t{ValueHelper.GetValue(key, name)}"),
                    });
                }
                catch (Exception exception)
                {
                    resultList.Add(new Result
                    {
                        ContextData = new RegistryEntry(key.Name, key, exception),
                        IcoPath = iconPath,
                        Title = exception.Message,
                        ToolTipData = new ToolTipData(exception.Message, exception.ToString()),
                    });
                }
            }

            return resultList;
        }

        #pragma warning restore CA1031 // Do not catch general exception types
    }
}
