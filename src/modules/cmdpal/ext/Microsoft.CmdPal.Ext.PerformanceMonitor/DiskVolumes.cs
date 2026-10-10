// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using CoreWidgetProvider.Helpers;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

/// <summary>
/// Describes the volumes of a physical disk. Disk counter instance names look like
/// <c>0 C: Z:</c>: the disk number followed by its drive letters.
/// </summary>
internal static class DiskVolumes
{
    private const double WarningPercent = 85;
    private const double AttentionPercent = 95;

    /// <summary>Returns the drive letters (such as <c>C:</c>) in a disk instance name.</summary>
    public static IReadOnlyList<string> GetDriveLetters(string instanceName)
    {
        var letters = new List<string>();
        foreach (var token in instanceName.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length == 2 && char.IsAsciiLetter(token[0]) && token[1] == ':')
            {
                letters.Add(token.ToUpperInvariant());
            }
        }

        return letters;
    }

    /// <summary>Formats <c>0 C: Z:</c> as <c>Disk 0 (C: Z:)</c>.</summary>
    public static string GetDisplayName(string instanceName)
    {
        if (!TryGetDiskNumber(instanceName, out var number))
        {
            return instanceName;
        }

        var letters = GetDriveLetters(instanceName);
        return letters.Count == 0
            ? string.Format(CultureInfo.CurrentCulture, Resources.GetResource("DiskUsage_Widget_Template/Disk_Number"), number)
            : string.Format(CultureInfo.CurrentCulture, Resources.GetResource("DiskUsage_Widget_Template/Disk_Number_Volumes"), number, string.Join(' ', letters));
    }

    /// <summary>Reads the disk number, such as 0, from an instance name such as <c>0 C: Z:</c>.</summary>
    public static bool TryGetDiskNumber(string instanceName, out int number)
    {
        number = 0;
        var parts = instanceName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
    }

    /// <summary>Describes a disk as Task Manager does, such as <c>SSD (NVMe)</c>, or returns null if unknown.</summary>
    public static string? GetTypeText(DiskDeviceInfo info)
    {
        var type = info.IsSolidState switch
        {
            true => Resources.GetResource("DiskUsage_Widget_Template/Type_SSD"),
            false => Resources.GetResource("DiskUsage_Widget_Template/Type_HDD"),
            null => null,
        };
        var bus = DiskDevices.GetBusName(info.BusType);
        return (type, bus) switch
        {
            (not null, not null) => string.Format(CultureInfo.CurrentCulture, Resources.GetResource("DiskUsage_Widget_Template/Type_With_Bus"), type, bus),
            (not null, null) => type,
            (null, not null) => bus,
            _ => null,
        };
    }

    /// <summary>Returns a capacity bar's color: accent, then warning and attention as the volume fills up.</summary>
    public static string GetCapacityColor(double usedPercent) => usedPercent switch
    {
        >= AttentionPercent => "attention",
        >= WarningPercent => "warning",
        _ => "accent",
    };

    /// <summary>Creates one entry per ready volume, for the card's capacity bars.</summary>
    public static JsonArray Create(string instanceName)
    {
        var volumes = new JsonArray();
        foreach (var letter in GetDriveLetters(instanceName))
        {
            try
            {
                var drive = new DriveInfo(letter);
                if (!drive.IsReady || drive.TotalSize <= 0)
                {
                    continue;
                }

                var usedPercent = (drive.TotalSize - drive.TotalFreeSpace) * 100.0 / drive.TotalSize;
                var name = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? letter : $"{drive.VolumeLabel} ({letter})";
                volumes.Add((JsonNode)new JsonObject
                {
                    ["name"] = name,
                    ["freeText"] = string.Format(
                        CultureInfo.CurrentCulture,
                        Resources.GetResource("DiskUsage_Widget_Template/Volume_Free"),
                        FormatBytes(drive.TotalFreeSpace),
                        FormatBytes(drive.TotalSize)),
                    ["usedPercent"] = Math.Round(usedPercent, 1),
                    ["color"] = GetCapacityColor(usedPercent),
                });
            }
            catch (Exception)
            {
                // Skip volumes that disappear or deny access while they're being read.
            }
        }

        return volumes;
    }

    /// <summary>Formats a size in binary units, the way File Explorer does.</summary>
    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Format(CultureInfo.CurrentCulture, value >= 100 || unit == 0 ? "{0:0} {1}" : "{0:0.#} {1}", value, units[unit]);
    }
}
