// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Microsoft.CmdPal.Ext.System.Helpers;

/// <summary>
/// This class represents the information for a network connection/interface
/// </summary>
internal sealed class NetworkConnectionProperties
{
    /// <summary>
    /// Decimal unicode value for green circle emoji.
    /// We need to generate it in the code because it does not render using Markdown emoji syntax or Unicode character syntax.
    /// </summary>
    /// <seealso cref="https://github.com/CommunityToolkit/Labs-Windows/blob/main/components/MarkdownTextBlock/samples/MarkdownTextBlock.md"/>
    /// <seealso cref="https://github.com/xoofx/markdig/blob/master/src/Markdig/Extensions/Emoji/EmojiMapping.cs"/>
    private const int GreenCircleCharacter = 128994;

    /// <summary>
    /// Gets the name of the adapter
    /// </summary>
    internal string Adapter { get; private set; }

    /// <summary>
    /// Gets the physical address (MAC) of the adapter
    /// </summary>
    internal string PhysicalAddress { get; private set; }

    /// <summary>
    /// Gets a value indicating the interface type
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
    internal string ConnectionName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a string with the suffix of the connection
    /// </summary>
    internal string Suffix { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the IPv4 address
    /// </summary>
    internal string IPv4 { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the IPv4 subnet mask
    /// </summary>
    internal string IPv4Mask { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the primarily used IPv6 address
    /// </summary>
    internal string IPv6Primary { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the global IPv6 address
    /// </summary>
    internal string IPv6Global { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the temporary IPv6 address
    /// </summary>
    internal string IPv6Temporary { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the link local IPv6 address
    /// </summary>
    internal string IPv6LinkLocal { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the site local IPv6 address
    /// </summary>
    internal string IPv6SiteLocal { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the unique local IPv6 address
    /// </summary>
    internal string IPv6UniqueLocal { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the list of gateway IPs as string
    /// </summary>
    internal List<IPAddress> Gateways { get; private set; } = new List<IPAddress>();

    /// <summary>
    /// Gets the list of DHCP server IPs as string
    /// </summary>
    internal IPAddressCollection? DhcpServers { get; private set; }

    /// <summary>
    /// Gets the list of DNS server IPs as string
    /// </summary>
    internal IPAddressCollection? DnsServers { get; private set; }

    /// <summary>
    /// Gets the list of WINS server IPs as string
    /// </summary>
    internal IPAddressCollection? WinsServers { get; private set; }

    private static readonly CompositeFormat MicrosoftPluginSysGbps = CompositeFormat.Parse(Resources.Microsoft_plugin_sys_Gbps);
    private static readonly CompositeFormat MicrosoftPluginSysMbps = CompositeFormat.Parse(Resources.Microsoft_plugin_sys_Mbps);

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkConnectionProperties"/> class.
    /// This private constructor is used when we crete the list of adapter (properties) by calling <see cref="NetworkConnectionProperties.GetList()"/>.
    /// </summary>
    /// <param name="networkInterface">Network interface of the connection</param>
    private NetworkConnectionProperties(NetworkInterface networkInterface)
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
    /// Creates a list with all network adapters and their properties
    /// </summary>
    /// <returns>List containing all network adapters</returns>
    internal static List<NetworkConnectionProperties> GetList()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                                         .Where(x => x.NetworkInterfaceType != NetworkInterfaceType.Loopback && x.GetPhysicalAddress() != null)
                                         .Select(i => new NetworkConnectionProperties(i))
                                         .OrderByDescending(i => i.IPv4) // list IPv4 first
                                         .ThenBy(i => i.IPv6Primary) // then IPv6
                                         .ToList();
        return interfaces;
    }

    /// <summary>
    /// Gets a formatted string with the adapter details
    /// </summary>
    /// <returns>String with the details</returns>
    internal string GetAdapterDetails()
    {
        return $"**{Resources.Microsoft_plugin_sys_AdapterName}:** {Adapter}" +
            $"\n\n**{Resources.Microsoft_plugin_sys_State}:** " + (State == OperationalStatus.Up ? char.ConvertFromUtf32(GreenCircleCharacter) + " " + Resources.Microsoft_plugin_sys_Connected : ":red_circle: " + Resources.Microsoft_plugin_sys_Disconnected) +
            $"\n\n**{Resources.Microsoft_plugin_sys_PhysicalAddress}:** {PhysicalAddress}" +
            $"\n\n**{Resources.Microsoft_plugin_sys_Speed}:** {GetFormattedSpeedValue(Speed)}" +
            $"\n\n**{Resources.Microsoft_plugin_sys_Type}:** {GetAdapterTypeAsString(Type)}" +
            $"\n\n**{Resources.Microsoft_plugin_sys_ConnectionName}:** {ConnectionName}";
    }

    /// <summary>
    /// Returns a formatted string with the connection details
    /// </summary>
    /// <returns>String with the details</returns>
    internal string GetConnectionDetails()
    {
        return $"**{Resources.Microsoft_plugin_sys_ConnectionName}:** {ConnectionName}" +
            $"\n\n**{Resources.Microsoft_plugin_sys_State}:** " + (State == OperationalStatus.Up ? char.ConvertFromUtf32(GreenCircleCharacter) + " " + Resources.Microsoft_plugin_sys_Connected : ":red_circle: " + Resources.Microsoft_plugin_sys_Disconnected) +
            $"\n\n**{Resources.Microsoft_plugin_sys_Type}:** {GetAdapterTypeAsString(Type)}" +
            $"\n\n**{Resources.Microsoft_plugin_sys_Suffix}:** {Suffix}" +
            CreateIpInfoForDetailsText($"**{Resources.Microsoft_plugin_sys_Ip4Address}:** ", IPv4) +
            CreateIpInfoForDetailsText($"**{Resources.Microsoft_plugin_sys_Ip4SubnetMask}:** ", IPv4Mask) +
            CreateIpInfoForDetailsText($"**{Resources.Microsoft_plugin_sys_Ip6Address}:**\n\n* ", IPv6Global) +
            CreateIpInfoForDetailsText($"**{Resources.Microsoft_plugin_sys_Ip6Temp}:**\n\n* ", IPv6Temporary) +
            CreateIpInfoForDetailsText($"**{Resources.Microsoft_plugin_sys_Ip6Link}:**\n\n* ", IPv6LinkLocal) +
            CreateIpInfoForDetailsText($"**{Resources.Microsoft_plugin_sys_Ip6Site}:**\n\n* ", IPv6SiteLocal) +
            CreateIpInfoForDetailsText($"**{Resources.Microsoft_plugin_sys_Ip6Unique}:**\n\n* ", IPv6UniqueLocal) +
            CreateIpInfoForDetailsText($"**{Resources.Microsoft_plugin_sys_Gateways}:**\n\n* ", Gateways) +
            CreateIpInfoForDetailsText($"**{Resources.Microsoft_plugin_sys_Dhcp}:**\n\n* ", DhcpServers == null ? string.Empty : DhcpServers) +
            CreateIpInfoForDetailsText($"**{Resources.Microsoft_plugin_sys_Dns}:**\n\n* ", DnsServers == null ? string.Empty : DnsServers) +
            CreateIpInfoForDetailsText($"**{Resources.Microsoft_plugin_sys_Wins}:**\n\n* ", WinsServers == null ? string.Empty : WinsServers) +
            $"\n\n**{Resources.Microsoft_plugin_sys_AdapterName}:** {Adapter}" +
            $"\n\n**{Resources.Microsoft_plugin_sys_PhysicalAddress}:** {PhysicalAddress}" +
            $"\n\n**{Resources.Microsoft_plugin_sys_Speed}:** {GetFormattedSpeedValue(Speed)}";
    }

    /// <summary>
    /// Set the ip address properties of the <see cref="NetworkConnectionProperties"/> instance.
    /// </summary>
    /// <param name="properties">Element of the type <see cref="IPInterfaceProperties"/>.</param>
    private void SetIpProperties(IPInterfaceProperties properties)
    {
        DateTime t = DateTime.Now;

        UnicastIPAddressInformationCollection ipList = properties.UnicastAddresses;
        GatewayIPAddressInformationCollection gwList = properties.GatewayAddresses;
        DhcpServers = properties.DhcpServerAddresses;
        DnsServers = properties.DnsAddresses;
        WinsServers = properties.WinsServersAddresses;

        for (var i = 0; i < ipList.Count; i++)
        {
            IPAddress ip = ipList[i].Address;

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                IPv4 = ip.ToString();
                IPv4Mask = ipList[i].IPv4Mask.ToString();
            }
            else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (string.IsNullOrEmpty(IPv6Primary))
                {
                    IPv6Primary = ip.ToString();
                }

                if (ip.IsIPv6LinkLocal)
                {
                    IPv6LinkLocal = ip.ToString();
                }
                else if (ip.IsIPv6SiteLocal)
                {
                    IPv6SiteLocal = ip.ToString();
                }
                else if (ip.IsIPv6UniqueLocal)
                {
                    IPv6UniqueLocal = ip.ToString();
                }
                else if (ipList[i].SuffixOrigin == SuffixOrigin.Random)
                {
                    IPv6Temporary = ip.ToString();
                }
                else
                {
                    IPv6Global = ip.ToString();
                }
            }
        }

        for (var i = 0; i < gwList.Count; i++)
        {
            Gateways.Add(gwList[i].Address);
        }

        Debug.Print($"time for getting ips: {DateTime.Now - t}");
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
    /// <param name="speed">The adapter speed as <see langword="long"/>.</param>
    /// <returns>A formatted string like `100 MB/s`</returns>
    private static string GetFormattedSpeedValue(long speed)
    {
        return (speed >= 1000000000) ? string.Format(CultureInfo.InvariantCulture, MicrosoftPluginSysGbps, speed / 1000000000) : string.Format(CultureInfo.InvariantCulture, MicrosoftPluginSysMbps, speed / 1000000);
    }

    /// <summary>
    /// Returns IP info or an empty string
    /// </summary>
    /// <param name="title">Descriptive header for the information.</param>
    /// <param name="property">IP value as <see cref="string"/> or <see cref="List{String}"/>.</param>
    /// <returns>Formatted string or an empty string.</returns>
    /// <exception cref="ArgumentException">If the parameter <paramref name="property"/> is not of the type <see cref="string"/> or <see cref="List{String}"/>.</exception>
    private static string CreateIpInfoForDetailsText(string title, dynamic property)
    {
        switch (property)
        {
            case string:
                return string.IsNullOrWhiteSpace(property) ? string.Empty : $"\n\n{title}{property}";
            case List<string> listString:
                return listString.Count == 0 ? string.Empty : $"\n\n{title}{string.Join("\n\n* ", property)}";
            case List<IPAddress> listIP:
                return listIP.Count == 0 ? string.Empty : $"\n\n{title}{string.Join("\n\n* ", property)}";
            case IPAddressCollection collectionIP:
                return collectionIP.Count == 0 ? string.Empty : $"\n\n{title}{string.Join("\n\n* ", property)}";
            case null:
                return string.Empty;
            default:
                throw new ArgumentException($"'{property}' is not of type 'string', 'List<string>', 'List<IPAddress>' or 'IPAddressCollection'.", nameof(property));
        }
    }
}
