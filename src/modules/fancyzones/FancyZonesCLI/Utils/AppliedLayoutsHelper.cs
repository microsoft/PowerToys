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

    internal enum DeviceMatchStrength
    {
        NoMatch,
        Legacy,
        MonitorNumber,
        MonitorInstance,
    }

    public static bool MatchesDevice(
        AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper device,
        string monitorName,
        string monitorInstance,
        string serialNumber,
        int monitorNumber,
        string virtualDesktop)
    {
        if (device.Monitor != monitorName || device.VirtualDesktop != virtualDesktop)
        {
            return false;
        }

        // If serial numbers are both available, they must match.
        if (!string.IsNullOrEmpty(device.SerialNumber) && !string.IsNullOrEmpty(serialNumber) &&
            device.SerialNumber != serialNumber)
        {
            return false;
        }

        return GetDeviceMatchStrength(device, monitorInstance, monitorNumber) != DeviceMatchStrength.NoMatch;
    }

    public static bool MatchesDeviceWithDefaultVirtualDesktop(
        AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper device,
        string monitorName,
        string monitorInstance,
        string serialNumber,
        int monitorNumber,
        string virtualDesktop)
    {
        if (device.VirtualDesktop == DefaultVirtualDesktopGuid)
        {
            // For this one layout record only, match any virtual desktop.
            return device.Monitor == monitorName &&
                GetDeviceMatchStrength(device, monitorInstance, monitorNumber) != DeviceMatchStrength.NoMatch;
        }

        return MatchesDevice(device, monitorName, monitorInstance, serialNumber, monitorNumber, virtualDesktop);
    }

    public static AppliedLayouts.AppliedLayoutWrapper? FindLayoutForMonitor(
        AppliedLayouts.AppliedLayoutsListWrapper layouts,
        string monitorName,
        string monitorInstance,
        string serialNumber,
        int monitorNumber,
        string virtualDesktop,
        IReadOnlyList<EditorParameters.NativeMonitorDataWrapper> connectedMonitors)
    {
        if (layouts.AppliedLayouts == null)
        {
            return null;
        }

        AppliedLayouts.AppliedLayoutWrapper? match = null;
        DeviceMatchStrength matchStrength = DeviceMatchStrength.NoMatch;
        foreach (var layout in layouts.AppliedLayouts)
        {
            if (!MatchesDevice(layout.Device, monitorName, monitorInstance, serialNumber, monitorNumber, virtualDesktop))
            {
                continue;
            }

            var candidateStrength = GetDeviceMatchStrength(layout.Device, monitorInstance, monitorNumber);
            if ((int)candidateStrength < (int)matchStrength)
            {
                continue;
            }

            if ((int)candidateStrength > (int)matchStrength)
            {
                match = layout;
                matchStrength = candidateStrength;
                continue;
            }

            // Multiple equally strong records do not identify one layout safely.
            return null;
        }

        if (!match.HasValue)
        {
            return null;
        }

        if (matchStrength == DeviceMatchStrength.Legacy &&
            CountConnectedMonitorsWithSameBaseIdentity(
                match.Value.Device,
                connectedMonitors) != 1)
        {
            return null;
        }

        return match;
    }

    internal static DeviceMatchStrength GetDeviceMatchStrength(
        AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper device,
        string monitorInstance,
        int monitorNumber)
    {
        if (!string.IsNullOrEmpty(device.MonitorInstance) && !string.IsNullOrEmpty(monitorInstance))
        {
            return device.MonitorInstance == monitorInstance
                ? DeviceMatchStrength.MonitorInstance
                : DeviceMatchStrength.NoMatch;
        }

        if (device.MonitorNumber > 0 && monitorNumber > 0)
        {
            return device.MonitorNumber == monitorNumber
                ? DeviceMatchStrength.MonitorNumber
                : DeviceMatchStrength.NoMatch;
        }

        // Older records may not have either identity field. Callers must handle this weaker match
        // carefully when more than one connected monitor shares the remaining identity.
        return DeviceMatchStrength.Legacy;
    }

    internal static bool HasSameBaseIdentity(
        AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper device,
        string monitorName,
        string serialNumber,
        string virtualDesktop)
    {
        return device.Monitor == monitorName &&
            device.VirtualDesktop == virtualDesktop &&
            (string.IsNullOrEmpty(device.SerialNumber) ||
                string.IsNullOrEmpty(serialNumber) ||
                device.SerialNumber == serialNumber);
    }

    private static int CountConnectedMonitorsWithSameBaseIdentity(
        AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper device,
        IReadOnlyList<EditorParameters.NativeMonitorDataWrapper> connectedMonitors)
    {
        int count = 0;
        foreach (var monitor in connectedMonitors)
        {
            if (HasSameBaseIdentity(
                device,
                monitor.Monitor,
                monitor.MonitorSerialNumber,
                monitor.VirtualDesktop))
            {
                count++;
            }
        }

        return count;
    }
}
