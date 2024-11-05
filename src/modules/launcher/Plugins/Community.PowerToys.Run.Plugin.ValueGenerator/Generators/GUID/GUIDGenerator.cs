// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
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

        public static Guid V7()
        {
            // A UUIDv7 looks like this (see https://www.rfc-editor.org/rfc/rfc9562#name-uuid-version-7)
            // unix_ts_ms:
            //   48-bit big-endian unsigned number of the Unix Epoch timestamp in milliseconds, bits 0 through 47 (octets 0-5).
            // ver:
            //   The 4-bit version field as defined by Section 4.2, set to 0b0111 (7), bits 48 through 51 of octet 6.
            // rand_a:
            //   12 bits of pseudorandom data to provide uniqueness, bits 52 through 63 (octets 6-7).
            // var:
            //   The 2-bit variant field as defined by Section 4.1, set to 0b10, bits 64 and 65 of octet 8.
            // rand_b:
            //   The final 62 bits of pseudorandom data to provide uniqueness, bits 66 through 127 (octets 8-15).
            Span<byte> buffer = stackalloc byte[16];

            // first, fill the whole buffer with cryptographically-secure pseudorandom data (because we don't know what users will use the generated values for).
            RandomNumberGenerator.Fill(buffer);

            // then, get unix_ts_ms. we need to write in big-endian, so shift the 64 bit value by 16 to get the actual timestamp into the upper 48 bits.
            ulong timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ulong timestamp48 = timestamp << 16;

            // bytes 6 through 9 (0-indexed) need special treatment as they contain the version followed by 12 bits of randomness, followed by the variant field
            // so we extract the existing random data and mask off the version and variant fields to get the correct format.
            // for the initial read, endianness won't matter because it's pre-filled with random data anyways
            // we read as LE for simplicity, because that's what most modern processors natively use
            uint bytes6To9 = BinaryPrimitives.ReadUInt32LittleEndian(buffer[6..10]);

            // version field: set upper-most nibble (byte 6) to value 7: (clear by AND-ing with 0, and set by OR-ing with 7).
            // rand_a: remains 12 bit of unchanged random data (AND-ing with 0xFFF, and OR-ing with 0x000)
            // var: the upper two bits of byte 8 are set to 0b10: (clear upper two bits by AND-ing with 0x3F, and set to 0b10 by OR-ing with 0x80)
            // rand_b (partial): the rest of the data shall remain the unchanged pre-filled random data (AND-ing with '1' and OR-ing with '0')
            uint bytes6To9Masked = (bytes6To9 & 0x0FFF3FFF) | 0x70008000;

            // obviously we need to write the timestamp first. It contains 48 bit of data, followed by 16 bit of zeros (from the shift operation)
            // therefore byte 6 and 7 will contains zeros after that first step. That's fine because we override that region with our masked-off version/variant data.
            // make sure to write as big-endian here!
            BinaryPrimitives.WriteUInt64BigEndian(buffer, timestamp48);
            BinaryPrimitives.WriteUInt32BigEndian(buffer[6..], bytes6To9Masked);
            return new Guid(buffer, bigEndian: true);
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
