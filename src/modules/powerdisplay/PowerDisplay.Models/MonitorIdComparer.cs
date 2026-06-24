// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Models;

/// <summary>
/// The single canonical equality policy for a monitor's stable Id (the DevicePath-based
/// <c>"\\?\DISPLAY#&lt;EdidId&gt;#&lt;instance&gt;"</c> string). Every dictionary, hash set,
/// and equality check keyed on a monitor Id MUST go through this type so the policy lives in
/// exactly one place.
/// </summary>
/// <remarks>
/// <para>
/// Ordinal and case-<b>insensitive</b>. A persisted Id is normally re-derived from the
/// QueryDisplayConfig DevicePath (<c>MonitorIdentity.FromDevicePath</c>), so the same physical
/// monitor reproduces a byte-identical Id across runs and case-sensitive matching happens to
/// work today. But the WMI brightness <c>InstanceName</c> and the DevicePath for the same panel
/// can differ in casing (already handled case-insensitively where they are joined), and the
/// DevicePath casing is not guaranteed stable across driver updates or GPU-route changes. To
/// avoid orphaning per-monitor settings on a mere casing change — and to keep one consistent
/// rule across the in-memory join and the persisted stores — Id casing is treated as
/// non-significant everywhere.
/// </para>
/// <para>
/// Lives in <c>PowerDisplay.Models</c> because it is the only project referenced by both the
/// discovery/persistence code (<c>PowerDisplay.Lib</c>) and the settings library
/// (<c>Settings.UI.Library</c> / <c>Settings.UI</c>) that key collections on a monitor Id.
/// </para>
/// </remarks>
public static class MonitorIdComparer
{
    /// <summary>
    /// Canonical comparer for monitor-Id-keyed <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>,
    /// <see cref="System.Collections.Generic.HashSet{T}"/>, and LINQ lookups.
    /// </summary>
    public static readonly StringComparer Instance = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Returns <see langword="true"/> when two monitor Ids denote the same monitor under the
    /// canonical policy. Use in place of the <c>==</c> operator when comparing monitor Ids.
    /// </summary>
    public static bool Equal(string? left, string? right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
