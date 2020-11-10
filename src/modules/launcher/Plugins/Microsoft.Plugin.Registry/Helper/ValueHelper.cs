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
                RegistryValueKind.None => $"{key.GetValue(valueName)} (No data type)",
                RegistryValueKind.Unknown => $"{key.GetValue(valueName)} (unsupported data type)",
                RegistryValueKind.String => $"{key.GetValue(valueName)} (REG_SZ)",
                RegistryValueKind.ExpandString => $"{key.GetValue(valueName)} (REG_EXPAND_SZ)",
                RegistryValueKind.MultiString => $"{key.GetValue(valueName)} (REG_MULTI_SZ)",

                // TODO: same format as editor
                RegistryValueKind.Binary => $"{key.GetValue(valueName)} (REG_BINARY)",

                // TODO: same format as editor
                RegistryValueKind.DWord => $"{key.GetValue(valueName)} (REG_DWORD)",

                // TODO: same format as editor
                RegistryValueKind.QWord => $"{key.GetValue(valueName)} (REG_QWORD)",

                _ => throw new ArgumentOutOfRangeException(nameof(valueName)),
            };
    }
}
