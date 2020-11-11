// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using Microsoft.Win32;

namespace Microsoft.Plugin.Registry.Helper
{
    internal static class ValueHelper
    {
        internal static string GetValue(in RegistryKey key, in string valueName, int maxLength = int.MaxValue)
        {
            var unformatedValue = key.GetValue(valueName);

            var value = key.GetValueKind(valueName) switch
            {
                RegistryValueKind.DWord => $"0x{unformatedValue:X8} ({(uint)(int)unformatedValue})",
                RegistryValueKind.QWord => $"0x{unformatedValue:X16} ({(ulong)(long)unformatedValue})",
                RegistryValueKind.Binary => (unformatedValue as byte[]).Aggregate(string.Empty, (current, singleByte) => $"{current} {singleByte:X2}"),
                _ => $"{unformatedValue}",
            };

            return value.Length > maxLength
                ? $"{value.Substring(0, maxLength)}..."
                : value;
        }

        internal static object GetType(RegistryKey key, string valueName)
            => key.GetValueKind(valueName) switch
            {
                RegistryValueKind.None => "No data type",
                RegistryValueKind.Unknown => "Unsupported data type",
                RegistryValueKind.String => "REG_SZ",
                RegistryValueKind.ExpandString => "REG_EXPAND_SZ",
                RegistryValueKind.MultiString => "REG_MULTI_SZ",
                RegistryValueKind.Binary => "REG_BINARY",
                RegistryValueKind.DWord => "REG_DWORD",
                RegistryValueKind.QWord => "REG_QWORD",
                _ => throw new ArgumentOutOfRangeException(nameof(valueName)),
            };
    }
}
