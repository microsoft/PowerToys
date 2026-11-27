// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable annotations

using System;

using Microsoft.Win32;

namespace EnvironmentVariablesUILib.Helpers
{
    internal interface IEnvironmentVariablesRegistryKey : IDisposable
    {
        string[] GetValueNames();

        object? GetValue(string name, object defaultValue, RegistryValueOptions options);

        void SetValue(string name, object value, RegistryValueKind kind);

        void DeleteValue(string name, bool throwOnMissingValue);
    }
}
