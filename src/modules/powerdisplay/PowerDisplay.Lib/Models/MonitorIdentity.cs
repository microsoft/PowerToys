// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

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
    /// Convert a WMI <c>WmiMonitorBrightness.InstanceName</c> (e.g.,
    /// "DISPLAY\BOE0900\4&amp;...&amp;UID111_0") into the same canonical
    /// <see cref="Monitor.Id"/> that <see cref="FromDevicePath"/> produces from the
    /// matching QueryDisplayConfig DevicePath for the same physical monitor — e.g.,
    /// "\\?\DISPLAY#BOE0900#4&amp;...&amp;UID111". Deriving the Id from each source and
    /// comparing is the join key for pairing WMI brightness instances with
    /// <c>MonitorDisplayInfo</c> entries; it stays unique even on dual-internal-panel
    /// devices (Yoga Book 9i, Zenbook Duo) where two panels share an EdidId but differ
    /// in PnP UID.
    /// </summary>
    /// <param name="instanceName">InstanceName of the form "DISPLAY\&lt;EdidId&gt;\&lt;instance&gt;_&lt;N&gt;". Null, empty, or not a three-segment InstanceName returns empty string.</param>
    public static string FromInstanceName(string? instanceName)
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

        // Strip the trailing "_N" WMI-instance suffix (e.g. "..._0", "..._12").
        var instanceSegment = parts[2];
        var underscore = instanceSegment.LastIndexOf('_');
        if (underscore > 0)
        {
            instanceSegment = instanceSegment[..underscore];
        }

        // Reshape into the canonical DevicePath-style Monitor.Id: a WMI InstanceName uses
        // "\" separators and omits the "\\?\" device-interface prefix, whereas a
        // QueryDisplayConfig DevicePath uses "#" separators with that prefix. parts[0] is
        // the enumerator ("DISPLAY"), reused rather than hardcoded.
        return $@"\\?\{parts[0]}#{parts[1]}#{instanceSegment}";
    }

    /// <summary>
    /// Extract the EDID PnP identifier (3-letter PNP manufacturer + 4-hex product code,
    /// e.g. <c>"DELD1A8"</c>) from either a new-format <c>Monitor.Id</c> or a raw
    /// <c>QueryDisplayConfig</c> DevicePath. The EdidId sits between the leading
    /// <c>"\\?\DISPLAY#"</c> and the next <c>#</c>, and is identical for every physical
    /// monitor of the same model — use it for "which model" crash correlation without
    /// leaking per-unit identifiers.
    /// </summary>
    /// <param name="monitorId">A Monitor.Id (no trailing <c>#{guid}</c>) or a raw DevicePath (with trailing <c>#{guid}</c>).</param>
    /// <returns>EdidId segment (e.g. <c>"DELD1A8"</c>), or empty string if the input is not a recognized form.</returns>
    public static string EdidIdFromMonitorId(string? monitorId)
    {
        if (string.IsNullOrEmpty(monitorId))
        {
            return string.Empty;
        }

        // Split: ["\\?\DISPLAY", "DELD1A8", "5&abc&0&UID12345"]
        var parts = monitorId.Split('#');
        if (parts.Length < 3 || string.IsNullOrEmpty(parts[1]))
        {
            return string.Empty;
        }

        return parts[1];
    }

    /// <summary>
    /// Return true if <paramref name="monitorId"/> matches the legacy
    /// <c>"{Source}_{EdidId}_{MonitorNumber}"</c> format produced by PowerDisplay
    /// before PR #47712 introduced the DevicePath-based Id.
    /// </summary>
    /// <remarks>
    /// Legacy sources are <c>DDC</c> and <c>WMI</c>; the monitor-number suffix is digits.
    /// EdidId may be the literal <c>"Unknown"</c> for monitors discovered without an EDID.
    /// </remarks>
    public static bool IsLegacyId(string? monitorId)
    {
        if (string.IsNullOrEmpty(monitorId))
        {
            return false;
        }

        if (!monitorId.StartsWith("DDC_", StringComparison.Ordinal)
            && !monitorId.StartsWith("WMI_", StringComparison.Ordinal))
        {
            return false;
        }

        var lastUnderscore = monitorId.LastIndexOf('_');
        if (lastUnderscore < 4 || lastUnderscore == monitorId.Length - 1)
        {
            return false;
        }

        // Trailing segment must be all digits (monitor number).
        for (int i = lastUnderscore + 1; i < monitorId.Length; i++)
        {
            if (!char.IsDigit(monitorId[i]))
            {
                return false;
            }
        }

        // Middle segment (EdidId) must be non-empty.
        return lastUnderscore - 4 > 0;
    }

    /// <summary>
    /// Extract the EdidId segment from a legacy <c>"{Source}_{EdidId}_{MonitorNumber}"</c> Id.
    /// Returns empty string when the input is not a legacy Id, or the EdidId is the literal
    /// <c>"Unknown"</c> placeholder which cannot be used for matching.
    /// </summary>
    public static string LegacyEdidId(string? monitorId)
    {
        if (!IsLegacyId(monitorId))
        {
            return string.Empty;
        }

        // monitorId = "DDC_DELD1A8_1" → take chars between first and last underscore.
        // "DDC_" and "WMI_" are both 4 chars; the first underscore is at index 3.
        var lastUnderscore = monitorId!.LastIndexOf('_');
        var edid = monitorId[4..lastUnderscore];
        return edid == "Unknown" ? string.Empty : edid;
    }

    /// <summary>
    /// Extract the trailing Windows DISPLAY number from a legacy
    /// <c>"{Source}_{EdidId}_{MonitorNumber}"</c> Id. Used as a disambiguator
    /// during migration: two physically identical monitors share an EdidId but
    /// have distinct DISPLAY numbers (1, 2, ...), so the legacy → new mapping is
    /// uniquely determined by the pair (EdidId, MonitorNumber).
    /// </summary>
    /// <returns>The monitor number, or 0 if the input is not a legacy Id or the number cannot be parsed.</returns>
    public static int LegacyMonitorNumber(string? monitorId)
    {
        if (!IsLegacyId(monitorId))
        {
            return 0;
        }

        var lastUnderscore = monitorId!.LastIndexOf('_');
        return int.TryParse(
            monitorId.AsSpan(lastUnderscore + 1),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var number) ? number : 0;
    }
}
