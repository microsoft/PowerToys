// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Models;

/// <summary>
/// Helpers for deriving the stable PowerDisplay <see cref="Monitor.Id"/> from
/// a Windows monitor DevicePath.
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
}
