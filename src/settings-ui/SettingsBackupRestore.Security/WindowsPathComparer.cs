// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

internal sealed class WindowsPathComparer : IComparer<string>, IEqualityComparer<string>
{
    internal static WindowsPathComparer Instance { get; } = new();

    public int Compare(string? left, string? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        int result = NativeMethods.CompareStringOrdinal(left, left.Length, right, right.Length, ignoreCase: true);
        if (result == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not compare normalized Windows archive paths.");
        }

        return result - 2;
    }

    internal bool EqualsPath(string left, string right)
    {
        return Compare(left, right) == 0;
    }

    public bool Equals(string? left, string? right)
    {
        return Compare(left, right) == 0;
    }

    public int GetHashCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return 0;
    }

    internal bool StartsWith(string value, string prefix)
    {
        if (value.Length < prefix.Length)
        {
            return false;
        }

        int result = NativeMethods.CompareStringOrdinal(value, prefix.Length, prefix, prefix.Length, ignoreCase: true);
        if (result == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not compare a normalized Windows path prefix.");
        }

        return result == 2;
    }
}
