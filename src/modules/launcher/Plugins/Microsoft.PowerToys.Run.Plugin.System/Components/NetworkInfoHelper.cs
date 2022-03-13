// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Microsoft.PowerToys.Run.Plugin.System.Components
{
    internal static class NetworkInfoHelper
    {
        internal static Dictionary<string, string> GetMainIpsForConnection(NetworkInterface adapter)
        {
            UnicastIPAddressInformationCollection adresses = adapter.GetIPProperties().UnicastAddresses;
            string ip4 = adresses.Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault().Address.ToString();
            string ip6 = adresses.Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetworkV6).FirstOrDefault().Address.ToString();

            return new Dictionary<string, string>()
            {
                { "IpV4", ip4 },
                { "IpV6", ip6 },
            };
        }

        internal static string GetConnectionDetails(NetworkInterface adapter)
        {
            return $"Name: {adapter.Name}" +
                $"\nState: " + (adapter.OperationalStatus == OperationalStatus.Up ? "Connected" : "Disconnected") +
                $"\nType: {GetAdapterType(adapter.NetworkInterfaceType)}" +
                $"\nSuffix: {adapter.GetIPProperties().DnsSuffix}" +
                "\nIP v4: {0}" +
                "\nIP v6: {1}" +
                $"\nAdapter: {adapter.Description}" +
                $"\nMAC: {adapter.GetPhysicalAddress()}" +
                $"\nSpeed: {FormatSpeedValue(adapter.Speed)}";
        }

        internal static string GetAdapterDetails(NetworkInterface adapter)
        {
            return $"Name: {adapter.Description}" +
                $"\nMAC: {adapter.GetPhysicalAddress()}" +
                $"\nSpeed: {FormatSpeedValue(adapter.Speed)}" +
                $"\nType: {GetAdapterType(adapter.NetworkInterfaceType)}" +
                $"\nState: " + (adapter.OperationalStatus == OperationalStatus.Up ? "Connected" : "Disconnected") +
                $"\nConnection: {adapter.Name}";
        }

        private static string GetAdapterType(NetworkInterfaceType type)
        {
            switch (type)
            {
                case NetworkInterfaceType.Wman:
                case NetworkInterfaceType.Wwanpp:
                case NetworkInterfaceType.Wwanpp2:
                    return "Mobile broadband";
                case NetworkInterfaceType.Wireless80211:
                    return "Wireless";
                case NetworkInterfaceType.Loopback:
                    return "Loopback";
                case NetworkInterfaceType.Tunnel:
                    return "Tunnel connection";
                case NetworkInterfaceType.Unknown:
                    return "Unknown";
                default:
                    return "Cable";
            }
        }

        private static string FormatSpeedValue(long speed)
        {
            return (speed >= 1000000000) ? (speed / 1000000000) + " Gb/s" : (speed / 1000000) + " Mb/s";
        }
    }
}
