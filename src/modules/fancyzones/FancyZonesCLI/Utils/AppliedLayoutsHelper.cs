// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesEditorCommon.Data;

namespace FancyZonesCLI.Utils;

/// <summary>
/// Helper for managing applied layouts across monitors.
/// CLI-only business logic for matching, finding, and updating applied layouts.
/// </summary>
internal static class AppliedLayoutsHelper
{
    public const string DefaultVirtualDesktopGuid = "{00000000-0000-0000-0000-000000000000}";

    public static bool MatchesDevice(
        AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper device,
        string monitorName,
        string serialNumber,
        int monitorNumber,
        string virtualDesktop)
    {
        // Must match monitor name
        if (device.Monitor != monitorName)
        {
            return false;
        }

        // Must match virtual desktop
        if (device.VirtualDesktop != virtualDesktop)
        {
            return false;
        }

        // If serial numbers are both available, they must match
        if (!string.IsNullOrEmpty(device.SerialNumber) && !string.IsNullOrEmpty(serialNumber))
        {
            if (device.SerialNumber != serialNumber)
            {
                return false;
            }
        }

        // If we reach here: Monitor name, VirtualDesktop, and SerialNumber (if available) all match
        // MonitorInstance and MonitorNumber can vary, so we accept any value
        return true;
    }

    public static bool MatchesDeviceWithDefaultVirtualDesktop(
        AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper device,
        string monitorName,
        string serialNumber,
        int monitorNumber,
        string virtualDesktop)
    {
        if (device.VirtualDesktop == DefaultVirtualDesktopGuid)
        {
            // For this one layout record only, match any virtual desktop.
            return device.Monitor == monitorName;
        }

        return MatchesDevice(device, monitorName, serialNumber, monitorNumber, virtualDesktop);
    }

    public static AppliedLayouts.AppliedLayoutWrapper? FindLayoutForMonitor(
        AppliedLayouts.AppliedLayoutsListWrapper layouts,
        string monitorName,
        string serialNumber,
        int monitorNumber,
        string virtualDesktop)
    {
        if (layouts.AppliedLayouts == null)
        {
            return null;
        }

        foreach (var layout in layouts.AppliedLayouts)
        {
            if (MatchesDevice(layout.Device, monitorName, serialNumber, monitorNumber, virtualDesktop))
            {
                return layout;
            }
        }

        return null;
    }
}
