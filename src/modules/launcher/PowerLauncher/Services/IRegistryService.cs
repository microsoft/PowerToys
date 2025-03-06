// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Win32;

namespace PowerLauncher.Services;

#nullable enable

/// <summary>
/// Provides methods for interacting with the Windows Registry or an equivalent key-value data
/// store.
/// </summary>
public interface IRegistryService
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
    /// <exception cref="ArgumentException"><paramref name="keyName"/> does not begin with a valid
    /// registry root.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown if access to the registry or
    /// equivalent store is denied.</exception>
    /// <remarks>Implementations may throw additional exceptions depending on their internal
    /// storage mechanism.</remarks>
    object? GetValue(string keyName, string? valueName, object? defaultValue);

    /// <summary>
    /// Sets the specified name/value pair on the specified registry key. If the specified key does
    /// not exist, it is created.
    /// </summary>
    /// <param name="keyName">The full registry path of the key, beginning with a valid registry
    /// root, such as "HKEY_CURRENT_USER".</param>
    /// <param name="valueName">The name of the name/value pair.</param>
    /// <param name="value">The value to be stored.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="keyName"/> does not begin with a valid registry root.
    ///
    /// -or-
    ///
    /// <paramref name="keyName"> is longer than the maximum length allowed (255 characters).
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Access to the key is denied; for example,
    /// it is a root-level node, or the key has not been opened with write access.</exception>
    void SetValue(string keyName, string? valueName, object value);

    /// <summary>
    /// Sets the specified name/value pair on the specified registry key. If the specified key does
    /// not exist, it is created.
    /// </summary>
    /// <param name="keyName">The full registry path of the key, beginning with a valid registry
    /// root, such as "HKEY_CURRENT_USER".</param>
    /// <param name="valueName">The name of the name/value pair.</param>
    /// <param name="value">The value to be stored.</param>
    /// <param name="valueKind">The registry data type to use when storing the data.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="keyName"/> does not begin with a valid registry root.
    ///
    /// -or-
    ///
    /// <paramref name="keyName"> is longer than the maximum length allowed (255 characters).
    ///
    /// -or-
    ///
    /// The type of <paramref name="value"/> did not match the registry data type specified by
    /// <paramref name="valueKind"/>, therefore the data could not be converted properly.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Access to the key is denied; for example,
    /// it is a root-level node, or the key has not been opened with write access.</exception>
    void SetValue(string keyName, string? valueName, object value, RegistryValueKind valueKind);
}
