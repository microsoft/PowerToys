// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Wox.Plugin.Common.Win32;

namespace Community.PowerToys.Run.Plugin.ValueGenerator.GUID
{
    internal sealed class GUIDGenerator
    {
        // As defined in https://datatracker.ietf.org/doc/html/rfc4122#appendix-C
        public static readonly Dictionary<string, Guid> PredefinedNamespaces = new Dictionary<string, Guid>()
        {
            { "ns:dns", new Guid(0x6ba7b810, 0x9dad, 0x11d1, 0x80, 0xb4, 0x00, 0xc0, 0x4f, 0xd4, 0x30, 0xc8) },
            { "ns:url", new Guid(0x6ba7b811, 0x9dad, 0x11d1, 0x80, 0xb4, 0x00, 0xc0, 0x4f, 0xd4, 0x30, 0xc8) },
            { "ns:oid", new Guid(0x6ba7b812, 0x9dad, 0x11d1, 0x80, 0xb4, 0x00, 0xc0, 0x4f, 0xd4, 0x30, 0xc8) },
            { "ns:x500", new Guid(0x6ba7b814, 0x9dad, 0x11d1, 0x80, 0xb4, 0x00, 0xc0, 0x4f, 0xd4, 0x30, 0xc8) },
        };

        public static Guid V1()
        {
            GUIDDATA guiddata;

            int uuidCreateResult = NativeMethods.UuidCreateSequential(out guiddata);

            if (uuidCreateResult != Win32Constants.RPC_S_OK && uuidCreateResult != Win32Constants.RPC_S_UUID_LOCAL_ONLY)
            {
                throw new InvalidOperationException("Failed to create GUID version 1");
            }

            return new Guid(guiddata.Data1, guiddata.Data2, guiddata.Data3, guiddata.Data4);
        }

        public static Guid V3(Guid uuidNamespace, string uuidName)
        {
            return V3AndV5(uuidNamespace, uuidName, 3);
        }

        public static Guid V4()
        {
            return Guid.NewGuid();
        }

        public static Guid V5(Guid uuidNamespace, string uuidName)
        {
            return V3AndV5(uuidNamespace, uuidName, 5);
        }

        private static Guid V3AndV5(Guid uuidNamespace, string uuidName, short version)
        {
            byte[] namespaceBytes = uuidNamespace.ToByteArray();
            byte[] networkEndianNamespaceBytes = namespaceBytes;

            // Convert time_low, time_mid and time_hi_and_version to network order
            int time_low = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(networkEndianNamespaceBytes.AsSpan()[0..4]));
            short time_mid = IPAddress.HostToNetworkOrder(BitConverter.ToInt16(networkEndianNamespaceBytes.AsSpan()[4..6]));
            short time_hi_and_version = IPAddress.HostToNetworkOrder(BitConverter.ToInt16(networkEndianNamespaceBytes.AsSpan()[6..8]));

            Buffer.BlockCopy(BitConverter.GetBytes(time_low), 0, networkEndianNamespaceBytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(time_mid), 0, networkEndianNamespaceBytes, 4, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(time_hi_and_version), 0, networkEndianNamespaceBytes, 6, 2);

            byte[] nameBytes = Encoding.ASCII.GetBytes(uuidName);

            byte[] namespaceAndNameBytes = new byte[networkEndianNamespaceBytes.Length + nameBytes.Length];
            Buffer.BlockCopy(networkEndianNamespaceBytes, 0, namespaceAndNameBytes, 0, namespaceBytes.Length);
            Buffer.BlockCopy(nameBytes, 0, namespaceAndNameBytes, networkEndianNamespaceBytes.Length, nameBytes.Length);

            byte[] hash;
            if (version == 3)
            {
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
                hash = MD5.HashData(namespaceAndNameBytes);
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms
            }
            else if (version == 5)
            {
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
                hash = SHA1.HashData(namespaceAndNameBytes);
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
            }
            else
            {
                throw new InvalidOperationException($"GUID version {version} does not exist");
            }

            byte[] result = new byte[16];

            // Copy first 16-bytes of the hash into our Uuid result
            Buffer.BlockCopy(hash, 0, result, 0, 16);

            // Convert put time_low, time_mid and time_hi_and_version back to host order
            time_low = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(result.AsSpan()[0..4]));
            time_mid = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(result.AsSpan()[4..6]));
            time_hi_and_version = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(result.AsSpan()[6..8]));

            // Set version 'version' in time_hi_and_version field according to https://datatracker.ietf.org/doc/html/rfc4122#section-4.1.3
            time_hi_and_version &= 0x0FFF;
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
            time_hi_and_version = (short)(time_hi_and_version | (version << 12));
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand

            Buffer.BlockCopy(BitConverter.GetBytes(time_low), 0, result, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(time_mid), 0, result, 4, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(time_hi_and_version), 0, result, 6, 2);

            // Set upper two bits to "10"
            result[8] &= 0x3F;
            result[8] |= 0x80;

            return new Guid(result);
        }
    }
}
