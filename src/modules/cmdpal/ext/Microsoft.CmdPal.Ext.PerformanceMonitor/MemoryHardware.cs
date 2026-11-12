// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Win32;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

/// <summary>
/// Reads the installed memory, which Task Manager uses to show hardware reserved memory.
/// It's read once, since it doesn't change while Windows runs.
/// </summary>
internal static class MemoryHardware
{
    private static readonly Lazy<ulong> CachedInstalledBytes = new(ReadInstalledBytes);

    /// <summary>Gets the installed memory in bytes, or zero when unknown.</summary>
    public static ulong InstalledBytes => CachedInstalledBytes.Value;

    private static ulong ReadInstalledBytes() =>
        PInvoke.GetPhysicallyInstalledSystemMemory(out var kilobytes) ? kilobytes * 1024 : 0;
}
