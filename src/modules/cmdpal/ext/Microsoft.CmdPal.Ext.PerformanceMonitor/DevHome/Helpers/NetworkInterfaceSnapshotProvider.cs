// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.NetworkInformation;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.Ext.PerformanceMonitor;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;
using Windows.Win32.NetworkManagement.Ndis;

namespace CoreWidgetProvider.Helpers;

/// <summary>
/// Reads Windows IP Helper interface tables and applies the configured adapter filters.
/// </summary>
internal sealed class NetworkInterfaceSnapshotProvider : INetworkInterfaceSnapshotProvider
{
    private readonly Func<HashSet<ulong>> _readIpInterfaceLuids;
    private HashSet<ulong> _ipInterfaceLuids = [];
    private bool _ipInterfaceReadFailureLogged;
    private volatile SettingsManager? _settingsManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkInterfaceSnapshotProvider"/> class.
    /// </summary>
    public NetworkInterfaceSnapshotProvider()
        : this(ReadIpInterfaceLuids)
    {
    }

    internal NetworkInterfaceSnapshotProvider(Func<HashSet<ulong>> readIpInterfaceLuids)
    {
        _readIpInterfaceLuids = readIpInterfaceLuids;
    }

    /// <summary>
    /// Sets the live settings source for subsequent snapshot reads.
    /// </summary>
    /// <param name="settingsManager">Adapter filter settings.</param>
    public void Configure(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    /// <inheritdoc/>
    /// <remarks>IP lookup failures use the last successful result, or an empty set before the first success.</remarks>
    /// <exception cref="Win32Exception">The interface table could not be read.</exception>
    public unsafe IReadOnlyList<NetworkInterfaceSnapshot> GetSnapshots()
    {
        var result = PInvoke.GetIfTable2(out var table);
        if (result != WIN32_ERROR.NO_ERROR)
        {
            throw new Win32Exception(unchecked((int)result));
        }

        if (table is null)
        {
            return [];
        }

        try
        {
            return GetSnapshots(table->Table.AsSpan(checked((int)table->NumEntries)));
        }
        finally
        {
            PInvoke.FreeMibTable(table);
        }
    }

    internal IReadOnlyList<NetworkInterfaceSnapshot> GetSnapshots(ReadOnlySpan<MIB_IF_ROW2> rows)
    {
        var settings = _settingsManager;
        var excludeVirtualAdapters = settings?.ExcludeVirtualNetworkAdapters ?? SettingsManager.DefaultExcludeVirtualNetworkAdapters;
        var excludeDisconnectedAdapters = settings?.ExcludeDisconnectedNetworkAdapters ?? SettingsManager.DefaultExcludeDisconnectedNetworkAdapters;
        var ipInterfaceLuids = excludeVirtualAdapters ? null : GetIpInterfaceLuids();
        var snapshots = new List<NetworkInterfaceSnapshot>(rows.Length);
        foreach (ref readonly var row in rows)
        {
            if (!ShouldInclude(row, ipInterfaceLuids?.Contains(row.InterfaceLuid.Value) == true, excludeVirtualAdapters, excludeDisconnectedAdapters))
            {
                continue;
            }

            var name = row.Alias.ToString();
            var description = row.Description.ToString();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = description;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = row.InterfaceGuid.ToString();
            }

            snapshots.Add(new(
                row.InterfaceLuid.Value,
                row.InterfaceGuid,
                name,
                row.InOctets,
                row.OutOctets,
                GetKnownLinkSpeed(row.OperStatus, row.ReceiveLinkSpeed, row.TransmitLinkSpeed),
                row.InterfaceAndOperStatusFlags.HardwareInterface)
            {
                Description = description,
                Type = (NetworkInterfaceType)row.Type,
                Status = (OperationalStatus)row.OperStatus,
            });
        }

        snapshots.Sort(static (left, right) =>
        {
            var nameComparison = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
            return nameComparison != 0 ? nameComparison : left.InterfaceLuid.CompareTo(right.InterfaceLuid);
        });

        return snapshots;
    }

    private HashSet<ulong> GetIpInterfaceLuids()
    {
        try
        {
            _ipInterfaceLuids = _readIpInterfaceLuids();
            _ipInterfaceReadFailureLogged = false;
        }
        catch (Exception ex)
        {
            if (!_ipInterfaceReadFailureLogged)
            {
                _ipInterfaceReadFailureLogged = true;
                CoreLogger.LogError("Failed to refresh IP interfaces. Keeping the previous virtual adapter list and continuing physical adapter updates.", ex);
            }
        }

        return _ipInterfaceLuids;
    }

    private static unsafe HashSet<ulong> ReadIpInterfaceLuids()
    {
        var result = PInvoke.GetIpInterfaceTable(ADDRESS_FAMILY.AF_UNSPEC, out var table);
        if (result == WIN32_ERROR.ERROR_NOT_FOUND)
        {
            return [];
        }

        if (result != WIN32_ERROR.NO_ERROR)
        {
            throw new Win32Exception(unchecked((int)result));
        }

        if (table is null)
        {
            return [];
        }

        try
        {
            HashSet<ulong> interfaceLuids = [];
            foreach (ref readonly var row in table->Table.AsSpan(checked((int)table->NumEntries)))
            {
                interfaceLuids.Add(row.InterfaceLuid.Value);
            }

            return interfaceLuids;
        }
        finally
        {
            PInvoke.FreeMibTable(table);
        }
    }

    internal static bool ShouldInclude(in MIB_IF_ROW2 row, bool ipEnabled, bool excludeVirtualAdapters, bool excludeDisconnectedAdapters)
    {
        var flags = row.InterfaceAndOperStatusFlags;

        // Internal WAN miniports and switch plumbing have no IP stack; their connections do.
        return !flags.FilterInterface
            && !flags.EndPointInterface
            && row.Type != (uint)NetworkInterfaceType.Loopback
            && (flags.HardwareInterface || (!excludeVirtualAdapters && ipEnabled))
            && (!excludeDisconnectedAdapters || row.OperStatus == IF_OPER_STATUS.IfOperStatusUp);
    }

    internal static ulong GetKnownLinkSpeed(IF_OPER_STATUS status, ulong receiveLinkSpeed, ulong transmitLinkSpeed)
    {
        // Disconnected adapters can still report their previous link speed.
        if (status != IF_OPER_STATUS.IfOperStatusUp)
        {
            return 0;
        }

        var knownReceiveLinkSpeed = receiveLinkSpeed == ulong.MaxValue ? 0 : receiveLinkSpeed;
        var knownTransmitLinkSpeed = transmitLinkSpeed == ulong.MaxValue ? 0 : transmitLinkSpeed;
        return Math.Max(knownReceiveLinkSpeed, knownTransmitLinkSpeed);
    }
}
