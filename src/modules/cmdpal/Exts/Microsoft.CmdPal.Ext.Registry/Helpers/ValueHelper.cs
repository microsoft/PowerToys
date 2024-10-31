// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

using Microsoft.CmdPal.Ext.Registry.Properties;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.Registry.Helpers;

/// <summary>
/// Helper class to easier work with values of a <see cref="RegistryKey"/>
/// </summary>
internal static class ValueHelper
{
    /// <summary>
    /// Return a human readable value data, of the given value name inside the given <see cref="RegistryKey"/>
    /// </summary>
    /// <param name="key">The <see cref="RegistryKey"/> that should contain the value name.</param>
    /// <param name="valueName">The name of the value.</param>
    /// <param name="maxLength">The maximum length for the human readable value.</param>
    /// <returns>A human readable value data.</returns>
    internal static string GetValue(in RegistryKey key, in string valueName, int maxLength = int.MaxValue)
    {
        var unformattedValue = key.GetValue(valueName);

        if (unformattedValue == null)
        {
            throw new InvalidOperationException($"Cannot proceed when {nameof(unformattedValue)} is null.");
        }

        var valueData = key.GetValueKind(valueName) switch
        {
            RegistryValueKind.DWord => $"0x{unformattedValue:X8} ({(uint)(int)unformattedValue})",
            RegistryValueKind.QWord => $"0x{unformattedValue:X16} ({(ulong)(long)unformattedValue})",
#pragma warning disable CS8604 // Possible null reference argument.
            RegistryValueKind.Binary => (unformattedValue as byte[]).Aggregate(string.Empty, (current, singleByte) => $"{current} {singleByte:X2}"),
#pragma warning restore CS8604 // Possible null reference argument.
            _ => $"{unformattedValue}",
        };

        return valueData.Length > maxLength
            ? $"{valueData.Substring(0, maxLength)}..."
            : valueData;
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
