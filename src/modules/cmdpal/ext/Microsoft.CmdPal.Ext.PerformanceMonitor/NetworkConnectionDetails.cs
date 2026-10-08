// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

/// <summary>
/// A network adapter's connection type and addresses, as Task Manager shows them. The Wi-Fi
/// network name isn't included: reading it needs location access.
/// </summary>
internal sealed record NetworkConnectionDetails(NetworkInterfaceType Type, string IPv4, string IPv6)
{
    /// <summary>
    /// Reads the details of the adapter with <paramref name="interfaceId"/>, or, without one, of
    /// the adapter that has a default gateway, which carries most traffic. Enumerating adapters
    /// takes milliseconds, so callers cache the result.
    /// </summary>
    public static NetworkConnectionDetails? Read(Guid? interfaceId)
    {
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (interfaceId is Guid id
                    ? Guid.TryParse(adapter.Id, out var adapterId) && adapterId == id
                    : IsDefaultConnection(adapter))
                {
                    var addresses = new List<IPAddress>();
                    foreach (var address in adapter.GetIPProperties().UnicastAddresses)
                    {
                        addresses.Add(address.Address);
                    }

                    return new(
                        adapter.NetworkInterfaceType,
                        PickAddress(addresses, AddressFamily.InterNetwork),
                        PickAddress(addresses, AddressFamily.InterNetworkV6));
                }
            }
        }
        catch (NetworkInformationException)
        {
            // Treat an adapter that can't be read as unknown.
        }

        return null;
    }

    /// <summary>Returns the resource key for an adapter type's name, or null for types the card doesn't name.</summary>
    public static string? GetTypeResourceKey(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Wireless80211 => "NetworkUsage_Widget_Template/Type_WiFi",
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT
            or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.Ethernet3Megabit => "NetworkUsage_Widget_Template/Type_Ethernet",
        NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2 => "NetworkUsage_Widget_Template/Type_Cellular",
        _ => null,
    };

    /// <summary>
    /// Picks the address to show for a family: a public (global) address first, then a private
    /// one, and a link-local address last, since it only works on the local network segment.
    /// </summary>
    internal static string PickAddress(IEnumerable<IPAddress> addresses, AddressFamily family)
    {
        IPAddress? best = null;
        var bestRank = int.MaxValue;
        foreach (var address in addresses)
        {
            if (address.AddressFamily != family || IPAddress.IsLoopback(address))
            {
                continue;
            }

            var rank = GetRank(address);
            if (rank < bestRank)
            {
                best = address;
                bestRank = rank;
            }
        }

        return best?.ToString() ?? string.Empty;
    }

    private static int GetRank(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal ? 2 : (bytes[0] & 0xE0) == 0x20 ? 0 : 1;
        }

        return bytes[0] == 169 && bytes[1] == 254 ? 2 : 0;
    }

    private static bool IsDefaultConnection(NetworkInterface adapter) =>
        adapter.OperationalStatus == OperationalStatus.Up
        && adapter.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
        && adapter.GetIPProperties().GatewayAddresses.Count > 0;
}
