// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Win32;

namespace PowerLauncher.Services;

#nullable enable

public class RegistryService : IRegistryService
{
    /// <summary>
    /// Retrieves the value associated with the specified name, in the specified registry key.
    /// If the name is not found in the specified key, returns the specified default value, or
    /// <c>null</c> if the specified key does not exist.
    /// </summary>
    /// <param name="keyName">The full registry path of the key, beginning with a valid registry
    /// root, such as "HKEY_CURRENT_USER".</param>
    /// <param name="valueName">The name of the name/value pair.</param>
    /// <param name="defaultValue">The value to return if <see cref="valueName"/> does not exist.
    /// </param>
    /// <returns><c>null</c> if the subkey specified by <paramref name="keyName"/> does not exist;
    /// otherwise, the value associated with <paramref name="valueName"/>, or
    /// <paramref name="defaultValue"/> if <paramref name="valueName"/> is not found.</returns>
    public object? GetValue(string keyName, string? valueName, object? defaultValue) => Registry.GetValue(keyName, valueName, defaultValue);

    /// <summary>
    /// Sets the specified name/value pair on the specified registry key. If the specified key does
    /// not exist, it is created.
    /// </summary>
    /// <param name="keyName">The full registry path of the key, beginning with a valid registry
    /// root, such as "HKEY_CURRENT_USER".</param>
    /// <param name="valueName">The name of the name/value pair.</param>
    /// <param name="value">The value to be stored.</param>
    public void SetValue(string keyName, string? valueName, object value) => throw new NotImplementedException();

    /// <summary>
    /// Sets the specified name/value pair on the specified registry key. If the specified key does
    /// not exist, it is created.
    /// </summary>
    /// <param name="keyName">The full registry path of the key, beginning with a valid registry
    /// root, such as "HKEY_CURRENT_USER".</param>
    /// <param name="valueName">The name of the name/value pair.</param>
    /// <param name="value">The value to be stored.</param>
    /// <param name="valueKind">The registry data type to use when storing the data.</param>
    public void SetValue(string keyName, string? valueName, object value, RegistryValueKind valueKind) => throw new NotImplementedException();
}
