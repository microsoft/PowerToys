// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Provides read-only access to an extension API object whose members can cross a process boundary.
/// </summary>
public interface IExtensionObject<out T>
    where T : class
{
    /// <summary>
    /// Gets the wrapped object, or null when the wrapper is empty. Calls to its members can block or throw.
    /// </summary>
    T? Unsafe { get; }

    /// <summary>
    /// Gets a value indicating whether the wrapper holds an object, without checking the extension process.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Unsafe))]
    bool HasValue { get; }
}
