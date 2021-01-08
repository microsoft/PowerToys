// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.Plugin.Registry.Properties;
using Microsoft.Win32;

namespace Microsoft.Plugin.Registry.Helper
{
    /// <summary>
    /// Helper class to easier work with values of a <see cref="RegistryKey"/>
    /// </summary>
    internal static class ValueHelper
    {
        /// <summary>
        /// Return a human readable value, of the given value name inside the given <see cref="RegistryKey"/>
        /// </summary>
        /// <param name="key">The <see cref="RegistryKey"/> that should contain the value name</param>
        /// <param name="valueName">The name of the value</param>
        /// <param name="maxLength">The maximum length for the human readable value</param>
        /// <returns>A human readable value</returns>
        internal static string GetValue(in RegistryKey key, in string valueName, int maxLength = int.MaxValue)
        {
            var unformattedValue = key.GetValue(valueName);

            var value = key.GetValueKind(valueName) switch
            {
                RegistryValueKind.DWord => $"0x{unformattedValue:X8} ({(uint)(int)unformattedValue})",
                RegistryValueKind.QWord => $"0x{unformattedValue:X16} ({(ulong)(long)unformattedValue})",
                RegistryValueKind.Binary => (unformattedValue as byte[]).Aggregate(string.Empty, (current, singleByte) => $"{current} {singleByte:X2}"),
                _ => $"{unformattedValue}",
            };

            return value.Length > maxLength
                ? $"{value.Substring(0, maxLength)}..."
                : value;
        }

        /// <summary>
        /// Return the registry type name of a given value name inside a given <see cref="RegistryKey"/>
        /// </summary>
        /// <param name="key">The <see cref="RegistryKey"/> that should contain the value name</param>
        /// <param name="valueName">The name of the value</param>
        /// <returns>A registry type name</returns>
        internal static object GetType(RegistryKey key, string valueName)
        {
            return key.GetValueKind(valueName) switch
            {
                RegistryValueKind.None => Resources.RegistryValueKindNone,
                RegistryValueKind.Unknown => Resources.RegistryValueKindUnknown,
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
}
