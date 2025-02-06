// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32;

namespace PowerLauncher.Services;

#nullable enable

public interface IRegistryService
{
    object? GetValue(string keyName, string? valueName, object? defaultValue);

    void SetValue(string keyName, string? valueName, object value);

    void SetValue(string keyName, string? valueName, object value, RegistryValueKind valueKind);
}
