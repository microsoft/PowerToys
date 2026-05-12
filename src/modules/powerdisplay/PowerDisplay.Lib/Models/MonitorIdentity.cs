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

    /// <summary>
    /// Extract the PnP hardware key from a DevicePath. The key identifies a physical
    /// monitor across both QueryDisplayConfig (DevicePath) and WMI (InstanceName)
    /// representations, so it is the right join key for pairing WMI brightness instances
    /// with MonitorDisplayInfo entries.
    /// </summary>
    /// <param name="devicePath">DevicePath of the form "\\?\DISPLAY#BOE0900#4&amp;...&amp;UID111#{guid}".</param>
    /// <returns>Canonical key "BOE0900#4&amp;...&amp;UID111", or empty string if extraction fails.</returns>
    public static string PnpHardwareKeyFromDevicePath(string? devicePath)
    {
        if (string.IsNullOrEmpty(devicePath))
        {
            return string.Empty;
        }

        // Split: ["\\?\DISPLAY", "BOE0900", "4&...&UID111", "{guid}"]
        var parts = devicePath.Split('#');
        if (parts.Length < 3 || string.IsNullOrEmpty(parts[1]) || string.IsNullOrEmpty(parts[2]))
        {
            return string.Empty;
        }

        return $"{parts[1]}#{parts[2]}";
    }

    /// <summary>
    /// Extract the PnP hardware key from a WMI InstanceName. Produces the same canonical
    /// form as <see cref="PnpHardwareKeyFromDevicePath"/> for the same physical device,
    /// enabling reliable one-step matching even on dual-internal-panel devices where
    /// two panels share an EdidId but differ in PnP UID.
    /// </summary>
    /// <param name="instanceName">InstanceName of the form "DISPLAY\BOE0900\4&amp;...&amp;UID111_0".</param>
    /// <returns>Canonical key "BOE0900#4&amp;...&amp;UID111", or empty string if extraction fails.</returns>
    public static string PnpHardwareKeyFromInstanceName(string? instanceName)
    {
        if (string.IsNullOrEmpty(instanceName))
        {
            return string.Empty;
        }

        // Split: ["DISPLAY", "BOE0900", "4&...&UID111_0"]
        var parts = instanceName.Split('\\');
        if (parts.Length < 3 || string.IsNullOrEmpty(parts[1]) || string.IsNullOrEmpty(parts[2]))
        {
            return string.Empty;
        }

        // Strip the trailing "_N" WMI-instance suffix (e.g. "..._0").
        var instanceSegment = parts[2];
        var underscore = instanceSegment.LastIndexOf('_');
        if (underscore > 0)
        {
            instanceSegment = instanceSegment[..underscore];
        }

        return $"{parts[1]}#{instanceSegment}";
    }
}
