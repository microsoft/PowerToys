// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Also compiled by Windows PowerShell 5.1. Keep this file C# 5 compatible.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Microsoft.MouseWithoutBorders.UITests
{
    public static class TcpSocketTable
    {
        [DllImport("iphlpapi.dll")]
        private static extern uint GetExtendedTcpTable(
            IntPtr table,
            ref int size,
            [MarshalAs(UnmanagedType.Bool)] bool order,
            uint addressFamily,
            uint tableClass,
            uint reserved);

        [SuppressMessage("Maintainability", "CA1507", Justification = "This source must compile with the C# 5 compiler in Windows PowerShell 5.1.")]
        [SuppressMessage("Maintainability", "CA1512", Justification = "Windows PowerShell 5.1 uses .NET Framework without ThrowIfNegativeOrZero.")]
        public static SocketInfo[] Read(int processId)
        {
            if (processId <= 0)
            {
                throw new ArgumentOutOfRangeException("processId");
            }

            var sockets = new List<SocketInfo>();
            ReadFamily(sockets, processId, 2);
            ReadFamily(sockets, processId, 23);
            return sockets.ToArray();
        }

        private static void ReadFamily(List<SocketInfo> sockets, int processId, uint family)
        {
            const uint insufficientBuffer = 122;
            const uint ownerPidAll = 5;
            int size = 0;
            uint result = GetExtendedTcpTable(IntPtr.Zero, ref size, false, family, ownerPidAll, 0);
            if (result != insufficientBuffer)
            {
                throw new Win32Exception((int)result, "Cannot determine the TCP owner table size.");
            }

            for (int attempt = 0; attempt < 4; attempt++)
            {
                if (size < sizeof(int))
                {
                    throw new InvalidDataException("Invalid TCP owner table size.");
                }

                int capacity = size;
                IntPtr table = Marshal.AllocHGlobal(capacity);
                try
                {
                    result = GetExtendedTcpTable(table, ref size, false, family, ownerPidAll, 0);
                    if (result == insufficientBuffer)
                    {
                        continue;
                    }

                    if (result != 0)
                    {
                        throw new Win32Exception((int)result);
                    }

                    // MIB_TCP[6]TABLE_OWNER_PID: DWORD count, followed by DWORD-aligned
                    // MIB_TCPROW_OWNER_PID (24 bytes) or MIB_TCP6ROW_OWNER_PID (56 bytes).
                    int stride = family == 2 ? 24 : 56;
                    int count = Marshal.ReadInt32(table);
                    if (count < 0 || sizeof(int) + ((long)count * stride) > capacity)
                    {
                        throw new InvalidDataException("TCP owner table rows exceed the returned buffer.");
                    }

                    for (int index = 0; index < count; index++)
                    {
                        IntPtr row = IntPtr.Add(table, sizeof(int) + (index * stride));
                        if (Marshal.ReadInt32(row, stride - sizeof(int)) != processId)
                        {
                            continue;
                        }

                        bool ipv4 = family == 2;
                        sockets.Add(new SocketInfo
                        {
                            LocalAddress = ReadAddress(row, ipv4 ? 4 : 0, ipv4),
                            LocalPort = ReadPort(row, ipv4 ? 8 : 20),
                            RemoteAddress = ReadAddress(row, ipv4 ? 12 : 24, ipv4),
                            RemotePort = ReadPort(row, ipv4 ? 16 : 44),
                            OwningProcess = processId,
                            State = ((TcpState)Marshal.ReadInt32(row, ipv4 ? 0 : 48)).ToString(),
                        });
                    }

                    return;
                }
                finally
                {
                    Marshal.FreeHGlobal(table);
                }
            }

            throw new Win32Exception((int)result, "TCP owner table kept growing during the bounded snapshot.");
        }

        private static string ReadAddress(IntPtr row, int offset, bool ipv4)
        {
            var bytes = new byte[ipv4 ? 4 : 16];
            Marshal.Copy(IntPtr.Add(row, offset), bytes, 0, bytes.Length);
            return ipv4 ? new IPAddress(bytes).ToString() :
                new IPAddress(bytes, unchecked((uint)Marshal.ReadInt32(row, offset + 16))).ToString();
        }

        private static int ReadPort(IntPtr row, int offset)
        {
            return (Marshal.ReadByte(row, offset) << 8) | Marshal.ReadByte(row, offset + 1);
        }

        public sealed class SocketInfo
        {
            public SocketInfo()
            {
                LocalAddress = string.Empty;
                RemoteAddress = string.Empty;
                State = string.Empty;
            }

            public string LocalAddress { get; set; }

            public int LocalPort { get; set; }

            public string RemoteAddress { get; set; }

            public int RemotePort { get; set; }

            public int OwningProcess { get; set; }

            public string State { get; set; }
        }
    }
}
