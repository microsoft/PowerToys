// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Net;

namespace PowerToys.TryRun.Core;

internal static class PolicyNetworkValidation
{
    public static void ValidatePeer(PolicyNetworkPeer peer)
    {
        var parent = ParseCidr(peer.Cidr);
        if (peer.Except is null || peer.Except.Length > 64)
        {
            throw new ArgumentException("Choose at most 64 destination exclusions.");
        }

        foreach (var excluded in peer.Except)
        {
            var child = ParseCidr(excluded);
            if (parent.Address.Length != child.Address.Length || child.Prefix < parent.Prefix ||
                !parent.Address.AsSpan(0, parent.Prefix / 8).SequenceEqual(child.Address.AsSpan(0, parent.Prefix / 8)))
            {
                throw new ArgumentException("Each excluded CIDR must belong to its destination network and use the same IP family.");
            }

            var remainder = parent.Prefix % 8;
            if (remainder != 0)
            {
                var mask = (byte)(255 << (8 - remainder));
                var index = parent.Prefix / 8;
                if ((parent.Address[index] & mask) != (child.Address[index] & mask))
                {
                    throw new ArgumentException("Each excluded CIDR must belong to its destination network.");
                }
            }
        }
    }

    public static void ValidateProxyAndHosts(PolicySettings settings, bool linux)
    {
        if (!linux && settings.Get("networkMode") == "Basic" && !settings.Enabled("allowOutbound") &&
            (settings.Lines("allowedHosts").Length != 0 || settings.Lines("blockedHosts").Length != 0))
        {
            throw new ArgumentException("Windows host rules require outbound network access to be enabled.");
        }

        var endpoint = settings.Get("networkProxy");
        var peer = settings.Get("allowedProxyPeer");
        if (peer.Length > 0 && (string.IsNullOrWhiteSpace(peer) || endpoint.Length == 0 || peer.Equals("MXC-Loopback", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("A proxy peer requires a runtime proxy URL and cannot use the reserved MXC-Loopback identity.");
        }

        if (endpoint.Length == 0)
        {
            return;
        }

        var uri = ValidateUrl(endpoint);
        var host = uri.Host.Trim('[', ']');
        if (!host.Equals("localhost", StringComparison.OrdinalIgnoreCase) && host != "127.0.0.1" && host != "::1")
        {
            throw new ArgumentException("The runtime proxy must use localhost, 127.0.0.1 or [::1].");
        }

        if (settings.Get("egressDefault") != "Deny" || settings.Rules("egressAllow").Length != 0 || settings.Rules("egressDeny").Length != 0)
        {
            throw new ArgumentException("A runtime proxy requires outbound Deny with no direct allow or deny rules.");
        }

        if (settings.Get("ingressDefault") != "Allow")
        {
            throw new ArgumentException("A Windows runtime proxy requires inbound Allow.");
        }

        if (settings.Get("hostLoopback") != (peer.Length == 0 ? "Allow" : "Deny"))
        {
            throw new ArgumentException("A runtime proxy without a peer requires host loopback Allow. A peer-scoped proxy requires host loopback Deny.");
        }
    }

    public static Uri ValidateUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not "http" and not "https" || string.IsNullOrEmpty(uri.Host))
        {
            throw new ArgumentException("Enter an absolute HTTP or HTTPS proxy URL.");
        }

        // The pinned native url parser drops explicit scheme-default ports before
        // MXC checks them. Mirror that restriction instead of accepting unusable URLs.
        if (uri.IsDefaultPort || uri.Port is < 1 or > 65535)
        {
            throw new ArgumentException("MXC requires an explicit proxy port other than the scheme default, for example 8080.");
        }

        return uri;
    }

    private static (byte[] Address, int Prefix) ParseCidr(string value)
    {
        var parts = value?.Split('/');
        if (parts is not { Length: 2 } || parts[0].Contains('%') ||
            !IPAddress.TryParse(parts[0], out var address) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var prefix) ||
            prefix > address.GetAddressBytes().Length * 8)
        {
            throw new ArgumentException("Network destinations and exclusions must be IP/CIDR ranges.");
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var octets = parts[0].Split('.');
            if (octets.Length != 4 || octets.Any(octet => octet.Length is < 1 or > 3 || !octet.All(char.IsAsciiDigit) || (octet.Length > 1 && octet[0] == '0')))
            {
                throw new ArgumentException("IPv4 CIDRs require four decimal octets without leading zeros.");
            }
        }

        var bytes = address.GetAddressBytes();
        for (var bit = prefix; bit < bytes.Length * 8; bit++)
        {
            if ((bytes[bit / 8] & (1 << (7 - (bit % 8)))) != 0)
            {
                throw new ArgumentException("CIDRs must use the network base address, with all host bits zero.");
            }
        }

        return (bytes, prefix);
    }
}
