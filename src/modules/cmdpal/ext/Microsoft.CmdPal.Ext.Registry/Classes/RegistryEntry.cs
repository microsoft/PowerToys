// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.CmdPal.Ext.Registry.Helpers;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.Registry.Classes;

/// <summary>
/// A entry of the registry.
/// </summary>
internal sealed class RegistryEntry
{
#pragma warning disable CS8632
    /// <summary>
    /// Gets the full path to a registry key.
    /// </summary>
    internal string KeyPath { get; }

    /// <summary>
    /// Gets the registry key for this entry.
    /// </summary>
    internal RegistryKey? Key { get; }

    /// <summary>
    /// Gets a possible exception that was occurred when try to open this registry key (e.g. <see cref="UnauthorizedAccessException"/>).
    /// </summary>
    internal Exception? Exception { get; }

    /// <summary>
    /// Gets the name of the current selected registry value.
    /// </summary>
    internal string? ValueName { get; }

    /// <summary>
    /// Gets the value of the current selected registry value.
    /// </summary>
    internal object? ValueData { get; }

#pragma warning restore CS8632

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryEntry"/> class.
    /// </summary>
    /// <param name="keyPath">The full path to the registry key for this entry.</param>
    /// <param name="exception">A exception that was occurred when try to access this registry key.</param>
    internal RegistryEntry(string keyPath, Exception exception)
    {
        KeyPath = keyPath;
        Exception = exception;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryEntry"/> class.
    /// </summary>
    /// <param name="key">The <see cref="RegistryKey"/> for this entry.</param>
    internal RegistryEntry(RegistryKey key)
    {
        KeyPath = key.Name;
        Key = key;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryEntry"/> class.
    /// </summary>
    /// <param name="key">The <see cref="RegistryKey"/> for this entry.</param>
    /// <param name="valueName">The value name of the current selected registry value.</param>
    /// <param name="value">The value of the current selected registry value.</param>
    internal RegistryEntry(RegistryKey key, string valueName, object value)
    {
        KeyPath = key.Name;
        Key = key;
        ValueName = valueName;
        ValueData = value;
    }

    /// <summary>
    /// Return the registry key.
    /// </summary>
    /// <returns>A registry key.</returns>
    internal string GetRegistryKey()
    {
        return $"{Key?.Name ?? KeyPath}";
    }

    /// <summary>
    /// Return the value name with the complete registry key.
    /// </summary>
    /// <returns>A value name with the complete registry key.</returns>
    internal string GetValueNameWithKey()
    {
        return $"{Key?.Name ?? KeyPath}\\\\{ValueName?.ToString() ?? string.Empty}";
    }

    /// <summary>
    /// Return the value data of a value name inside a registry key.
    /// </summary>
    /// <returns>A value data.</returns>
    internal string GetValueData()
    {
        if (Key is null)
        {
            return KeyPath;
        }

        if (string.IsNullOrEmpty(ValueName))
        {
            return Key.Name;
        }

        return ValueHelper.GetValue(Key, ValueName);
    }
}
