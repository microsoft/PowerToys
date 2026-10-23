// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Marks an object that the caller knows is owned by this process.
/// This does not validate ownership or make an out-of-process object safe to access.
/// Use a nullable type argument when the value can be null.
/// </summary>
public class InProcObject<T>(T value)
{
    public T Safe { get; } = value;

    public override bool Equals(object? obj) => obj is InProcObject<T> other && other.Safe?.Equals(Safe) == true;

    public override int GetHashCode() => Safe?.GetHashCode() ?? base.GetHashCode();
}
