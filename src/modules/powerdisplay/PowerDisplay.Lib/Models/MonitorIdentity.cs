// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Models;

/// <summary>
/// Helpers for deriving the stable PowerDisplay <see cref="Monitor.Id"/> from
/// a Windows monitor DevicePath, and for extracting fields out of the resulting
/// Id string.
/// </summary>
/// <remarks>
/// The Id format is the DevicePath returned by QueryDisplayConfig with the
/// trailing device-class GUID suffix stripped, e.g.,
/// "\\?\DISPLAY#DELD1A8#5&amp;abc123&amp;0&amp;UID12345". The middle segment
/// is the Windows PnP instance ID, unique per (physical device × physical port).
/// </remarks>
public static class MonitorIdentity
{
    /// <summary>
    /// Convert a Windows DevicePath (e.g., "\\?\DISPLAY#DELD1A8#5&amp;abc&amp;0&amp;UID1#{guid}")
    /// into the canonical Monitor.Id form by stripping the trailing "#{guid}" suffix.
    /// </summary>
    /// <param name="devicePath">A device path from <c>MonitorDisplayInfo.DevicePath</c>. Null or empty input returns empty string.</param>
    public static string FromDevicePath(string? devicePath)
    {
        if (string.IsNullOrEmpty(devicePath))
        {
            return string.Empty;
        }

        var guidStart = devicePath.IndexOf("#{", System.StringComparison.Ordinal);
        return guidStart < 0 ? devicePath : devicePath[..guidStart];
    }

    /// <summary>
    /// Extract the EdidId segment (manufacturer + product code) from a new-format Monitor.Id.
    /// Returns false if the Id is not in the new format.
    /// </summary>
    public static bool TryGetEdidId(string? monitorId, out string edidId)
    {
        edidId = string.Empty;
        if (string.IsNullOrEmpty(monitorId))
        {
            return false;
        }

        // New format begins with "\\?\DISPLAY#" — the EdidId is the segment between the
        // first and second '#'.
        var first = monitorId.IndexOf('#');
        if (first < 0)
        {
            return false;
        }

        var second = monitorId.IndexOf('#', first + 1);
        if (second < 0)
        {
            return false;
        }

        edidId = monitorId.Substring(first + 1, second - first - 1);
        return edidId.Length > 0;
    }
}
