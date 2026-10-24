// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Marks an object that the caller knows is owned by this process.
/// This does not validate ownership or make an out-of-process object safe to access.
/// Use a nullable type argument when the value can be null.
/// </summary>
public class InProcObject<T>(T value) : IEquatable<InProcObject<T>>
    where T : class
{
    /// <summary>
    /// Gets the wrapped object, which is an extension object owned by this process,
    /// or <see langword="null"/> when the wrapper is empty.
    /// </summary>
    public T Safe { get; } = value;

    /// <summary>
    /// Gets a value indicating whether the wrapper holds an object. It does not check that the
    /// extension behind it is still running.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Safe))]
    public bool HasValue => Safe is not null;

    /// <inheritdoc/>
    public bool Equals(InProcObject<T>? other) => other is not null && ReferenceEquals(Safe, other.Safe);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as InProcObject<T>);

    /// <inheritdoc/>
    public override int GetHashCode() => Safe?.GetHashCode() ?? base.GetHashCode();
}
