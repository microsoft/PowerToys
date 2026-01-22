// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using VARTYPE = System.Runtime.InteropServices.VarEnum;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
[StructLayout(LayoutKind.Explicit, Pack = 8)]
public struct PropVariant
{
    /// <summary>Value type tag.</summary>
    [FieldOffset(0)]
    public ushort vt;

    [FieldOffset(2)]
    public ushort wReserved1;

    /// <summary>Reserved for future use.</summary>
    [FieldOffset(4)]
    public ushort wReserved2;

    /// <summary>Reserved for future use.</summary>
    [FieldOffset(6)]
    public ushort wReserved3;

    /// <summary>The decimal value when VT_DECIMAL.</summary>
    [FieldOffset(0)]
    internal decimal _decimal;

    /// <summary>The raw data pointer.</summary>
    [FieldOffset(8)]
    internal IntPtr _ptr;

    /// <summary>The FILETIME when VT_FILETIME.</summary>
    [FieldOffset(8)]
    internal System.Runtime.InteropServices.ComTypes.FILETIME _ft;

    [FieldOffset(8)]
    internal BLOB _blob;

    /// <summary>The value when a numeric value less than 8 bytes.</summary>
    [FieldOffset(8)]
    internal ulong _ulong;

    public PropVariant(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        vt = (ushort)VarEnum.VT_LPWSTR;
        _ptr = Marshal.StringToCoTaskMemUni(value);
    }

    public VarEnum VarType { get => (VarEnum)vt; set => vt = (ushort)(VARTYPE)value; }
}

[StructLayout(LayoutKind.Sequential, Pack = 0)]
public struct BLOB
{
    /// <summary>The count of bytes</summary>
    public uint cbSize;

    /// <summary>A pointer to the allocated array of bytes.</summary>
    public IntPtr pBlobData;
}

#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
