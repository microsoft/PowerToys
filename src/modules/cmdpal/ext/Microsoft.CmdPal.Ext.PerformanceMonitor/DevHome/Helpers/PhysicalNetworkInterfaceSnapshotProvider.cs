// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.NetworkManagement.IpHelper;

namespace CoreWidgetProvider.Helpers;

internal sealed class PhysicalNetworkInterfaceSnapshotProvider : IPhysicalNetworkInterfaceSnapshotProvider
{
    public unsafe IReadOnlyList<PhysicalNetworkInterfaceSnapshot> GetSnapshots()
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
            var snapshots = new List<PhysicalNetworkInterfaceSnapshot>(checked((int)table->NumEntries));
            foreach (ref readonly var row in table->Table.AsSpan(checked((int)table->NumEntries)))
            {
                var flags = row.InterfaceAndOperStatusFlags;
                if (!flags.HardwareInterface || flags.FilterInterface || flags.EndPointInterface)
                {
                    continue;
                }

                var name = row.Description.ToString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = row.Alias.ToString();
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
                    GetKnownLinkSpeed(row.ReceiveLinkSpeed, row.TransmitLinkSpeed)));
            }

            snapshots.Sort(static (left, right) =>
            {
                var nameComparison = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
                return nameComparison != 0 ? nameComparison : left.InterfaceLuid.CompareTo(right.InterfaceLuid);
            });

            return snapshots;
        }
        finally
        {
            PInvoke.FreeMibTable(table);
        }
    }

    internal static ulong GetKnownLinkSpeed(ulong receiveLinkSpeed, ulong transmitLinkSpeed)
    {
        var knownReceiveLinkSpeed = receiveLinkSpeed == ulong.MaxValue ? 0 : receiveLinkSpeed;
        var knownTransmitLinkSpeed = transmitLinkSpeed == ulong.MaxValue ? 0 : transmitLinkSpeed;
        return Math.Max(knownReceiveLinkSpeed, knownTransmitLinkSpeed);
    }
}

internal readonly record struct PhysicalNetworkInterfaceSnapshot(
    ulong InterfaceLuid,
    Guid InterfaceGuid,
    string Name,
    ulong ReceivedBytes,
    ulong SentBytes,
    ulong LinkSpeed);

internal interface IPhysicalNetworkInterfaceSnapshotProvider
{
    IReadOnlyList<PhysicalNetworkInterfaceSnapshot> GetSnapshots();
}
