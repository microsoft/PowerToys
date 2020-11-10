// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Win32;

namespace Microsoft.Plugin.Registry.Helper
{
    internal static class ValueHelper
    {
        internal static string GetValue(in RegistryKey key, in string valueName)
            => key.GetValueKind(valueName) switch
            {
                RegistryValueKind.None => $"{key.GetValue(valueName)}",
                RegistryValueKind.Unknown => $"{key.GetValue(valueName)}",
                RegistryValueKind.String => $"{key.GetValue(valueName)}",
                RegistryValueKind.ExpandString => $"{key.GetValue(valueName)}",
                RegistryValueKind.MultiString => $"{key.GetValue(valueName)}",

                // TODO: same format as editor
                RegistryValueKind.Binary => $"{key.GetValue(valueName)}",

                // TODO: same format as editor
                RegistryValueKind.DWord => $"{key.GetValue(valueName)}",

                // TODO: same format as editor
                RegistryValueKind.QWord => $"{key.GetValue(valueName)}",

                _ => throw new ArgumentOutOfRangeException(nameof(valueName)),
            };

        internal static object GetType(RegistryKey key, string valueName)
            => key.GetValueKind(valueName) switch
            {
                RegistryValueKind.None => "No data type",
                RegistryValueKind.Unknown => "unsupported data type",
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
