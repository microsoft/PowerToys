// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerToysExtension.Helpers;

internal static class FancyZonesCommandIds
{
    private const string ApplyLayoutPrefix = "com.microsoft.powertoys.fancyzones.layout.apply:";
    private const string AllMonitorsSuffix = ":all";
    private const string MonitorMarker = ":monitor:";

    public static string BuildApplyLayoutCommandId(FancyZonesLayoutDescriptor layout, FancyZonesMonitorDescriptor? monitor)
    {
        var escapedLayoutId = Uri.EscapeDataString(layout.Id);
        return monitor is null
            ? $"{ApplyLayoutPrefix}{escapedLayoutId}{AllMonitorsSuffix}"
            : $"{ApplyLayoutPrefix}{escapedLayoutId}{MonitorMarker}{Uri.EscapeDataString(GetMonitorToken(monitor.Value))}";
    }

    public static bool TryParseApplyLayoutCommandId(string id, out string layoutId, out string? monitorToken)
    {
        layoutId = string.Empty;
        monitorToken = null;

        if (string.IsNullOrWhiteSpace(id) || !id.StartsWith(ApplyLayoutPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var payload = id[ApplyLayoutPrefix.Length..];
        if (payload.EndsWith(AllMonitorsSuffix, StringComparison.Ordinal))
        {
            var layoutPayload = payload[..^AllMonitorsSuffix.Length];
            if (string.IsNullOrWhiteSpace(layoutPayload))
            {
                return false;
            }

            try
            {
                layoutId = Uri.UnescapeDataString(layoutPayload);
            }
            catch (ArgumentException)
            {
                layoutId = string.Empty;
                return false;
            }

            return !string.IsNullOrWhiteSpace(layoutId);
        }

        var monitorMarkerIndex = payload.IndexOf(MonitorMarker, StringComparison.Ordinal);
        if (monitorMarkerIndex <= 0 || monitorMarkerIndex == payload.Length - MonitorMarker.Length)
        {
            return false;
        }

        var layoutPart = payload[..monitorMarkerIndex];
        var monitorPart = payload[(monitorMarkerIndex + MonitorMarker.Length)..];
        if (string.IsNullOrWhiteSpace(layoutPart) || string.IsNullOrWhiteSpace(monitorPart))
        {
            return false;
        }

        try
        {
            layoutId = Uri.UnescapeDataString(layoutPart);
            monitorToken = Uri.UnescapeDataString(monitorPart);
        }
        catch (ArgumentException)
        {
            layoutId = string.Empty;
            monitorToken = null;
            return false;
        }

        return !string.IsNullOrWhiteSpace(layoutId) && !string.IsNullOrWhiteSpace(monitorToken);
    }

    public static string GetMonitorToken(FancyZonesMonitorDescriptor monitor)
    {
        if (!string.IsNullOrWhiteSpace(monitor.Data.MonitorInstanceId))
        {
            return $"instance:{monitor.Data.MonitorInstanceId}";
        }

        return $"fallback:{monitor.Data.Monitor}|{monitor.Data.MonitorNumber}";
    }
}
