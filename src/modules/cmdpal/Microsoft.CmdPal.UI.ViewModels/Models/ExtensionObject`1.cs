// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Holds a reference to an object supplied through the Command Palette extension API. This is
/// usually a COM proxy for an object living in an extension's process; built-in extensions
/// and Command Palette's own adapters (such as <see cref="TopLevelViewModel"/>) supply
/// in-process objects instead.
/// </summary>
/// <remarks>
/// <para>
/// Reading <see cref="Unsafe"/> is cheap and never throws. Calling a member on the returned
/// object may cross a process boundary. Calls may block or throw, including after the extension
/// process exits, so callers must guard them.
/// </para>
/// <para>
/// Two wrappers are equal when they hold the same managed reference or are both empty. Equality
/// and hashing never call into the wrapped object. The same extension object can still be
/// reachable through different proxies, which compare unequal: CsWinRT usually reuses the proxy
/// it already created for a COM object, but that is a cache, not a guarantee (for example, when
/// the object reaches us through a different apartment).
/// </para>
/// </remarks>
public sealed class ExtensionObject<T>(T? value) : IExtensionObject<T>, IEquatable<ExtensionObject<T>>
    where T : class
{
    /// <summary>
    /// Gets the wrapped object, usually a COM proxy for an object in an extension's process,
    /// or <see langword="null"/> when the wrapper is empty.
    /// </summary>
    public T? Unsafe { get; } = value;

    /// <summary>
    /// Gets a value indicating whether the wrapper holds an object. It does not check that the
    /// extension behind it is still running.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Unsafe))]
    public bool HasValue => Unsafe is not null;

    /// <inheritdoc/>
    public bool Equals(ExtensionObject<T>? other) => other is not null && ReferenceEquals(Unsafe, other.Unsafe);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ExtensionObject<T>);

    /// <inheritdoc/>
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(Unsafe);
}
