// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Security;
using Microsoft.Win32;

namespace PowerLauncher.Services;

#nullable enable

public class RegistryService : IRegistryService
{
    /// <inheritdoc/>
    /// <exception cref="SecurityException">The user does not have the permissions required to read
    /// from the registry key.</exception>
    /// <exception cref="IOException">The <see cref="RegistryKey"/> that contains the specified
    /// value has been marked for deletion.</exception>
    public object? GetValue(string keyName, string? valueName, object? defaultValue) =>
        Registry.GetValue(keyName, valueName, defaultValue);

    /// <inheritdoc/>
    /// <exception cref="SecurityException">The user does not have the permissions required to
    /// create or modify registry keys.</exception>"
    public void SetValue(string keyName, string? valueName, object value) =>
        Registry.SetValue(keyName, valueName, value);

    /// <inheritdoc/>
    /// <exception cref="SecurityException">The user does not have the permissions required to
    /// create or modify registry keys.</exception>
    public void SetValue(string keyName, string? valueName, object value, RegistryValueKind valueKind) =>
        Registry.SetValue(keyName, valueName, value, valueKind);
}
