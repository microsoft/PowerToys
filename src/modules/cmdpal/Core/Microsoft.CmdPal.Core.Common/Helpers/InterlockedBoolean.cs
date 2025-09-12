// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;

namespace Microsoft.CmdPal.Core.Common.Helpers;

/// <summary>
/// Thread-safe boolean implementation using atomic operations
/// </summary>
public struct InterlockedBoolean(bool initialValue = false)
{
    private int _value = initialValue ? 1 : 0;

    /// <summary>
    /// Gets or sets the boolean value atomically
    /// </summary>
    public bool Value
    {
        get => Volatile.Read(ref _value) == 1;
        set => Interlocked.Exchange(ref _value, value ? 1 : 0);
    }

    /// <summary>
    /// Atomically sets the value to true
    /// </summary>
    /// <returns>True if the value was previously false, false if it was already true</returns>
    public bool Set()
    {
        return Interlocked.Exchange(ref _value, 1) == 0;
    }

    /// <summary>
    /// Atomically sets the value to false
    /// </summary>
    /// <returns>True if the value was previously true, false if it was already false</returns>
    public bool Clear()
    {
        return Interlocked.Exchange(ref _value, 0) == 1;
    }

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();
}
