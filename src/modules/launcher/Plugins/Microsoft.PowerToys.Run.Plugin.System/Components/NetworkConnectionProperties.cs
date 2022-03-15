// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.PowerToys.Run.Plugin.System.Properties;

namespace Microsoft.PowerToys.Run.Plugin.System.Components
{
    /// <summary>
    /// This class represents the informations for a network connection/interface
    /// </summary>
    internal class NetworkConnectionProperties
    {
        /// <summary>
        /// Gets the name of the adapter
        /// </summary>
        internal string Adapter { get; private set; }

        /// <summary>
        /// Gets the physical address (MAC) of the adapter
        /// </summary>
        internal string PhysicalAddress { get; private set; }

        /// <summary>
        /// Gets a value indicatin the interface type
        /// </summary>
        internal NetworkInterfaceType Type { get; private set; }

        /// <summary>
        /// Gets the speed of the adapter as unformatted value (Static information form the adapter device)
        /// </summary>
        internal long Speed { get; private set; }

        /// <summary>
        /// Gets a value indicating the operational state of the adapter
        /// </summary>
        internal OperationalStatus State { get; private set; }

        /// <summary>
        /// Gets the name of the network connection
        /// </summary>
        internal string ConnectionName { get; private set; }

        /// <summary>
        /// Gets a string with the suffix of the connection
        /// </summary>
        internal string Suffix { get; private set; }

        /// <summary>
        /// Gets the IP v4 address
        /// </summary>
        internal string IPv4 { get; private set; }

        /// <summary>
        /// Gets the IP v4 subnet mask
        /// </summary>
        internal string IPv4Mask { get; private set; }

        /// <summary>
        /// Gets the primarily used IP v6 address
        /// </summary>
        internal string IPv6Primary { get; private set; }

        /// <summary>
        /// Gets the global IP v6 address
        /// </summary>
        internal string IPv6Global { get; private set; }

        /// <summary>
        /// Gets the temporary IP v6 address
        /// </summary>
        internal string IPv6Temporary { get; private set; }

        /// <summary>
        /// Gets the link local IP v6 address
        /// </summary>
        internal string IPv6LinkLocal { get; private set; }

        /// <summary>
        /// Gets the site local IP v6 address
        /// </summary>
        internal string IPv6SiteLocal { get; private set; }

        /// <summary>
        /// Gets the unique local IP v6 address
        /// </summary>
        internal string IPv6UniqueLocal { get; private set; }

        /// <summary>
        /// Gets the list of gateway IPs as string
        /// </summary>
        internal List<string> Gateways { get; private set; } = new List<string>();

        /// <summary>
        /// Gets the list of DHCP server IPs as string
        /// </summary>
        internal List<string> DhcpServers { get; private set; } = new List<string>();

        /// <summary>
        /// Gets the list of DNS server IPs as string
        /// </summary>
        internal List<string> DnsServers { get; private set; } = new List<string>();

        /// <summary>
        /// Gets the list of WINS server IPs as string
        /// </summary>
        internal List<string> WinsServers { get; private set; } = new List<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkConnectionProperties"/> class.
        /// </summary>
        /// <param name="networkInterface">Network interface of the connection</param>
        internal NetworkConnectionProperties(NetworkInterface networkInterface)
        {
            // Setting adapter properties
            Adapter = networkInterface.Description;
            PhysicalAddress = networkInterface.GetPhysicalAddress().ToString();
            Type = networkInterface.NetworkInterfaceType;
            Speed = networkInterface.Speed;
            State = networkInterface.OperationalStatus;

            // Connection properties
            ConnectionName = networkInterface.Name;
            if (State == OperationalStatus.Up)
            {
                Suffix = networkInterface.GetIPProperties().DnsSuffix;
                SetIpProperties(networkInterface.GetIPProperties());
            }
        }

        /// <summary>
        /// Gets a formatted string with the adapter details
        /// </summary>
        /// <returns>String with the details</returns>
        internal string GetAdapterDetails()
        {
            return $"{Resources.Microsoft_plugin_sys_AdapterName}: {Adapter}" +
                $"\n{Resources.Microsoft_plugin_sys_PhysicalAddress}: {PhysicalAddress}" +
                $"\n{Resources.Microsoft_plugin_sys_Speed}: {GetFormattedSpeedValue(Speed)}" +
                $"\n{Resources.Microsoft_plugin_sys_Type}: {GetAdapterTypeAsString(Type)}" +
                $"\n{Resources.Microsoft_plugin_sys_State}: " + (State == OperationalStatus.Up ? Resources.Microsoft_plugin_sys_Connected : Resources.Microsoft_plugin_sys_Disconnected) +
                $"\n{Resources.Microsoft_plugin_sys_ConnectionName}: {ConnectionName}";
        }

        /// <summary>
        /// Returns a formatted string with the connection details
        /// </summary>
        /// <returns>String with the details</returns>
        internal string GetConnectionDetails()
        {
            return $"{Resources.Microsoft_plugin_sys_ConnectionName}: {ConnectionName}" +
                $"\n{Resources.Microsoft_plugin_sys_State}: " + (State == OperationalStatus.Up ? Resources.Microsoft_plugin_sys_Connected : Resources.Microsoft_plugin_sys_Disconnected) +
                $"\n{Resources.Microsoft_plugin_sys_Type}: {GetAdapterTypeAsString(Type)}" +
                $"\n{Resources.Microsoft_plugin_sys_Suffix}: {Suffix}" +
                CreateIpInfoForToolTip($"{Resources.Microsoft_plugin_sys_Ip4Address}: ", IPv4) +
                CreateIpInfoForToolTip($"{Resources.Microsoft_plugin_sys_Ip4SubnetMask}: ", IPv4Mask) +
                CreateIpInfoForToolTip($"{Resources.Microsoft_plugin_sys_Ip6Address}:\n\t", IPv6Global) +
                CreateIpInfoForToolTip($"{Resources.Microsoft_plugin_sys_Ip6Temp}:\n\t", IPv6Temporary) +
                CreateIpInfoForToolTip($"{Resources.Microsoft_plugin_sys_Ip6Link}:\n\t", IPv6LinkLocal) +
                CreateIpInfoForToolTip($"{Resources.Microsoft_plugin_sys_Ip6Site}:\n\t", IPv6SiteLocal) +
                CreateIpInfoForToolTip($"{Resources.Microsoft_plugin_sys_Ip6Unique}:\n\t", IPv6UniqueLocal) +
                CreateIpInfoForToolTip($"{Resources.Microsoft_plugin_sys_Gateways}:\n\t", Gateways) +
                CreateIpInfoForToolTip($"{Resources.Microsoft_plugin_sys_Dhcp}:\n\t", DhcpServers) +
                CreateIpInfoForToolTip($"{Resources.Microsoft_plugin_sys_Dns}:\n\t", DnsServers) +
                CreateIpInfoForToolTip($"{Resources.Microsoft_plugin_sys_Wins}:\n\t", WinsServers) +
                $"\n\n{Resources.Microsoft_plugin_sys_AdapterName}: {Adapter}" +
                $"\n{Resources.Microsoft_plugin_sys_PhysicalAddress}: {PhysicalAddress}" +
                $"\n{Resources.Microsoft_plugin_sys_Speed}: {GetFormattedSpeedValue(Speed)}";
        }

        /// <summary>
        /// Set the ip address properties of the <see cref="NetworkConnectionProperties"/> instance.
        /// </summary>
        /// <param name="properties">Element of the type <see cref="IPInterfaceProperties"/>.</param>
        private void SetIpProperties(IPInterfaceProperties properties)
        {
            var ipList = properties.UnicastAddresses;

            foreach (var ip in ipList)
            {
                if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    IPv4 = ip.Address.ToString();
                    IPv4Mask = ip.IPv4Mask.ToString();
                }
                else if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    if (string.IsNullOrEmpty(IPv6Primary))
                    {
                        IPv6Primary = ip.Address.ToString();
                    }

                    if (ip.Address.IsIPv6LinkLocal)
                    {
                        IPv6LinkLocal = ip.Address.ToString();
                    }
                    else if (ip.Address.IsIPv6SiteLocal)
                    {
                        IPv6SiteLocal = ip.Address.ToString();
                    }
                    else if (ip.Address.IsIPv6UniqueLocal)
                    {
                        IPv6UniqueLocal = ip.Address.ToString();
                    }
                    else if (ip.SuffixOrigin == SuffixOrigin.Random)
                    {
                       IPv6Temporary = ip.Address.ToString();
                    }
                    else
                    {
                        IPv6Global = ip.Address.ToString();
                    }
                }
            }

            foreach (var ip in properties.GatewayAddresses)
            {
                Gateways.Add(ip.Address.ToString());
            }

            foreach (var ip in properties.DhcpServerAddresses)
            {
                DhcpServers.Add(ip.ToString());
            }

            foreach (var ip in properties.DnsAddresses)
            {
                DnsServers.Add(ip.ToString());
            }

            foreach (var ip in properties.WinsServersAddresses)
            {
                WinsServers.Add(ip.ToString());
            }
        }

        /// <summary>
        /// Gets the interface type as string
        /// </summary>
        /// <param name="type">The type to convert</param>
        /// <returns>A string indicating the interface type</returns>
        private string GetAdapterTypeAsString(NetworkInterfaceType type)
        {
            switch (type)
            {
                case NetworkInterfaceType.Wman:
                case NetworkInterfaceType.Wwanpp:
                case NetworkInterfaceType.Wwanpp2:
                    return Resources.Microsoft_plugin_sys_MobileBroadband;
                case NetworkInterfaceType.Wireless80211:
                    return Resources.Microsoft_plugin_sys_WirelessLan;
                case NetworkInterfaceType.Loopback:
                    return Resources.Microsoft_plugin_sys_Loopback;
                case NetworkInterfaceType.Tunnel:
                    return Resources.Microsoft_plugin_sys_TunnelConnection;
                case NetworkInterfaceType.Unknown:
                    return Resources.Microsoft_plugin_sys_Unknown;
                default:
                    return Resources.Microsoft_plugin_sys_Cable;
            }
        }

        /// <summary>
        /// Gets the speed as formatted text value
        /// </summary>
        /// <returns>A formatted string like `100 MB/s`</returns>
        private static string GetFormattedSpeedValue(long speed)
        {
            return (speed >= 1000000000) ? string.Format(CultureInfo.InvariantCulture, Resources.Microsoft_plugin_sys_mbps, speed / 1000000000) : string.Format(CultureInfo.InvariantCulture, Resources.Microsoft_plugin_sys_mbps, speed / 1000000);
        }

        /// <summary>
        /// Returns IP info or an empty string
        /// </summary>
        /// <param name="title">Descriptive header for the information.</param>
        /// <param name="property">IP value as <see cref="string"/> or <see cref="List{String}"/>.</param>
        /// <returns>Formatted string or an empty string.</returns>
        /// <exception cref="ArgumentException">If the parameter <paramref name="property"/> is not of the type <see cref="string"/> or <see cref="List{String}"/>.</exception>
        private static string CreateIpInfoForToolTip(string title, dynamic property)
        {
            if (property is string)
            {
                return $"\n{title}{property}";
            }
            else if (property is List<string> list)
            {
                return list.Count == 0 ? string.Empty : $"\n{title}{string.Join("\n\t", property)}";
            }
            else if (property is null)
            {
                return string.Empty;
            }
            else
            {
                throw new ArgumentException("Parameter is not of type 'string' or 'List<string>'.", nameof(property));
            }
        }
    }
}
