// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Win32;
using Wox.Plugin;

namespace Microsoft.Plugin.Registry.Helper
{
    internal static class ResultHelper
    {
        #pragma warning disable CA1031 // Do not catch general exception types

        internal static List<Result> GetResultList(ICollection<(string, RegistryKey?, Exception?)> list, string iconPath)
        {
            var resultList = new List<Result>();

            foreach (var item in list)
            {
                var result = new Result
                {
                    IcoPath = iconPath,
                };

                if (item.Item3 is null && !(item.Item2 is null))
                {
                    // when key contains keys or fields
                    result.Title = item.Item2.Name;
                    result.SubTitle = RegistryHelper.GetSummary(item.Item2);
                    result.ContextData = item.Item2;
                }
                else if (item.Item2 is null && !(item.Item3 is null))
                {
                    // on error (e.g access denied)
                    result.Title = item.Item1;
                    result.SubTitle = item.Item3.Message;
                }
                else
                {
                    result.Title = item.Item1;
                }

                result.ToolTipData = new ToolTipData("Registry key", $"Key:\t{result.Title}");

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

            try
            {
                var resultList = new List<Result>();

                foreach (var name in key.GetValueNames())
                {
                    var result = new Result
                    {
                        ContextData = key,
                        IcoPath = iconPath,
                        SubTitle = $"{ValueHelper.GetValue(key, name)} ({ValueHelper.GetType(key, name)})",
                        Title = name,
                        ToolTipData = new ToolTipData("Registry key", $"Key:\t{key.Name}\nName:\t{name}\nValue:\t{ValueHelper.GetValue(key, name)}\nType:\t{ValueHelper.GetType(key, name)}"),
                    };

                    resultList.Add(result);
                }

                return resultList;
            }
            catch (Exception exception)
            {
                return new List<Result>
                {
                    new Result
                    {
                        ContextData = key,
                        IcoPath = iconPath,
                        Title = exception.ToString(),
                    },
                };
            }
        }

        #pragma warning restore CA1031 // Do not catch general exception types
    }
}
