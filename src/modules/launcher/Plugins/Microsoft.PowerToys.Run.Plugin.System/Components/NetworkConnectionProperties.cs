// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;

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
        internal string Gateways { get; private set; }

        /// <summary>
        /// Gets the list of DHCP server IPs as string
        /// </summary>
        internal string DhcpServers { get; private set; }

        /// <summary>
        /// Gets the list of DNS server IPs as string
        /// </summary>
        internal string DnsServers { get; private set; }

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
            return $"Name: {Adapter}" +
                $"\nPhysical address (MAC): {PhysicalAddress}" +
                $"\nSpeed: {GetFormattedSpeedValue(Speed)}" +
                $"\nType: {GetAdapterType(Type)}" +
                $"\nState: " + (State == OperationalStatus.Up ? "Connected" : "Disconnected") +
                $"\nConnection: {ConnectionName}";
        }

        /// <summary>
        /// Returns a formatted string with the connection details
        /// </summary>
        /// <returns>String with the details</returns>
        internal string GetConnectionDetails()
        {
            return $"Name: {ConnectionName}" +
                $"\nState: " + (State == OperationalStatus.Up ? "Connected" : "Disconnected") +
                $"\nType: {GetAdapterType(Type)}" +
                $"\nSuffix: {Suffix}" +
                $"\nIP v4 address: {IPv4}" +
                $"\nIP v4 subnet mask: {IPv4Mask}" +
                $"\nIP v6 address: {IPv6Global}" +
                $"\nIP v6 temporary address: {IPv6Temporary}" +
                $"\nIP v6 link local address: {IPv6LinkLocal}" +
                $"\nIP v6 site local address: {IPv6SiteLocal}" +
                $"\nIP v6 unique local address: {IPv6UniqueLocal}" +
                $"\nGateway addresses: {Gateways}" +
                $"\nDHCP server addresses: {DhcpServers}" +
                $"\nDNS server addresses: {DnsServers}" +
                $"\n\nAdapter: {Adapter}" +
                $"\nPhysical address (MAC): {PhysicalAddress}" +
                $"\nSpeed: {GetFormattedSpeedValue(Speed)}";
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
                Gateways += "\n\t" + ip.Address.ToString();
            }

            foreach (var ip in properties.DhcpServerAddresses)
            {
                DhcpServers += "\n\t" + ip.ToString();
            }

            foreach (var ip in properties.DnsAddresses)
            {
                DnsServers += "\n\t" + ip.ToString();
            }
        }

        /// <summary>
        /// Gets the interface type as string
        /// </summary>
        /// <param name="type">The type to convert</param>
        /// <returns>A string indicating the interface type</returns>
        private string GetAdapterType(NetworkInterfaceType type)
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

        /// <summary>
        /// Gets the speed as formatted text value
        /// </summary>
        /// <returns>A formatted string like `100 MB/s`</returns>
        private static string GetFormattedSpeedValue(long speed)
        {
            return (speed >= 1000000000) ? (speed / 1000000000) + " Gb/s" : (speed / 1000000) + " Mb/s";
        }
    }
}
