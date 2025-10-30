// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

// In X64, we are WOW
[module: SuppressMessage("Microsoft.Portability", "CA1900:ValueTypeFieldsShouldBePortable", Scope = "type", Target = "MouseWithoutBorders.Core.DATA", Justification = "Dotnet port with style preservation")]

// <summary>
//     Package format/conversion.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
namespace MouseWithoutBorders.Core;

// The beauty of "union" in C#
[StructLayout(LayoutKind.Explicit)]
internal sealed class DATA
{
    [FieldOffset(0)]
    internal PackageType Type; // 4 (first byte = package type, 1 = checksum, 2+3 = magic no.)

    [FieldOffset(sizeof(PackageType))]
    internal int Id; // 4

    [FieldOffset(sizeof(PackageType) + sizeof(uint))]
    internal ID Src; // 4

    [FieldOffset(sizeof(PackageType) + (2 * sizeof(uint)))]
    internal ID Des; // 4

    [FieldOffset(sizeof(PackageType) + (3 * sizeof(uint)))]
    internal long DateTime;

    [FieldOffset(sizeof(PackageType) + (3 * sizeof(uint)) + sizeof(long))]
    internal KEYBDDATA Kd;

    [FieldOffset(sizeof(PackageType) + (3 * sizeof(uint)))]
    internal MOUSEDATA Md;

    [FieldOffset(sizeof(PackageType) + (3 * sizeof(uint)))]
    internal ID Machine1;

    [FieldOffset(sizeof(PackageType) + (4 * sizeof(uint)))]
    internal ID Machine2;

    [FieldOffset(sizeof(PackageType) + (5 * sizeof(uint)))]
    internal ID Machine3;

    [FieldOffset(sizeof(PackageType) + (6 * sizeof(uint)))]
    internal ID Machine4;

    [FieldOffset(sizeof(PackageType) + (3 * sizeof(uint)))]
    internal ClipboardPostAction PostAction;

    [FieldOffset(sizeof(PackageType) + (7 * sizeof(uint)))]
    private long machineNameP1;

    [FieldOffset(sizeof(PackageType) + (7 * sizeof(uint)) + sizeof(long))]
    private long machineNameP2;

    [FieldOffset(sizeof(PackageType) + (7 * sizeof(uint)) + (2 * sizeof(long)))]
    private long machineNameP3;

    [FieldOffset(sizeof(PackageType) + (7 * sizeof(uint)) + (3 * sizeof(long)))]
    private long machineNameP4;

    internal string MachineName
    {
        get
        {
            string name = Common.GetString(BitConverter.GetBytes(machineNameP1))
                + Common.GetString(BitConverter.GetBytes(machineNameP2))
                + Common.GetString(BitConverter.GetBytes(machineNameP3))
                + Common.GetString(BitConverter.GetBytes(machineNameP4));
            return name.Trim();
        }

        set
        {
            byte[] machineName = Common.GetBytes(value.PadRight(32, ' '));
            machineNameP1 = BitConverter.ToInt64(machineName, 0);
            machineNameP2 = BitConverter.ToInt64(machineName, 8);
            machineNameP3 = BitConverter.ToInt64(machineName, 16);
            machineNameP4 = BitConverter.ToInt64(machineName, 24);
        }
    }

    public DATA()
    {
    }

    public DATA(byte[] initialData)
    {
        Bytes = initialData;
    }

    internal byte[] Bytes
    {
        get
        {
            byte[] buf = new byte[IsBigPackage ? Package.PACKAGE_SIZE_EX : Package.PACKAGE_SIZE];
            Array.Copy(StructToBytes(this), buf, IsBigPackage ? Package.PACKAGE_SIZE_EX : Package.PACKAGE_SIZE);

            return buf;
        }

        set
        {
            Debug.Assert(value.Length <= Package.PACKAGE_SIZE_EX, "Length > package size");
            byte[] buf = new byte[Package.PACKAGE_SIZE_EX];
            Array.Copy(value, buf, value.Length);
            BytesToStruct(buf, this);
        }
    }

    internal bool IsBigPackage
    {
        get => Type == 0
                ? throw new InvalidOperationException("Package type not set.")
                : Type switch
                {
                    PackageType.Hello or PackageType.Awake or PackageType.Heartbeat or PackageType.Heartbeat_ex or PackageType.Handshake or PackageType.HandshakeAck or PackageType.ClipboardPush or PackageType.Clipboard or PackageType.ClipboardAsk or PackageType.ClipboardImage or PackageType.ClipboardText or PackageType.ClipboardDataEnd => true,
                    _ => (Type & PackageType.Matrix) == PackageType.Matrix,
                };
    }

    private byte[] StructToBytes(object structObject)
    {
        byte[] bytes = new byte[Package.PACKAGE_SIZE_EX];
        GCHandle bHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        Marshal.StructureToPtr(structObject, Marshal.UnsafeAddrOfPinnedArrayElement(bytes, 0), false);
        bHandle.Free();
        return bytes;
    }

    private void BytesToStruct(byte[] value, object structObject)
    {
        GCHandle bHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
        Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(value, 0), structObject);
        bHandle.Free();
    }
}
