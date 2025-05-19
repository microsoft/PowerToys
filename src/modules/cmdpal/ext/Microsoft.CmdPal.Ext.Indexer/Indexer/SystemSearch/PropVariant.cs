// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using VARTYPE = System.Runtime.InteropServices.VarEnum;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[StructLayout(LayoutKind.Explicit, Pack = 8, CharSet = CharSet.Unicode)]
public struct PropVariant
{
    /// <summary>Value type tag.</summary>
    [FieldOffset(0)]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "No, please do not change the name")]
    public ushort vt;

    [FieldOffset(2)]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "No, please do not change the name")]
    public ushort wReserved1;

    /// <summary>Reserved for future use.</summary>
    [FieldOffset(4)]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "No, please do not change the name")]
    public ushort wReserved2;

    /// <summary>Reserved for future use.</summary>
    [FieldOffset(6)]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "No, please do not change the name")]
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

    /// <summary>The value when a numeric value less than 8 bytes.</summary>
    [FieldOffset(8)]
    internal ulong _ulong;

    public PropVariant()
    {
    }

    public PropVariant(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        vt = (ushort)VarEnum.VT_LPWSTR;
        _ptr = Marshal.StringToCoTaskMemUni(value);
    }

    public VarEnum VarType { get => (VarEnum)vt; set => vt = (ushort)(VARTYPE)value; }
}

[CustomMarshaller(typeof(PropVariant), MarshalMode.UnmanagedToManagedOut, typeof(PROPVARIANTOutMarshaller))]
[CustomMarshaller(typeof(PropVariant), MarshalMode.ManagedToUnmanagedOut, typeof(PROPVARIANTOutMarshaller))]
public static partial class PROPVARIANTOutMarshaller
{
    public static void Free(IntPtr managed)
    {
        // Marshal.FreeCoTaskMem(managed);
    }

    public static unsafe PropVariant ConvertToManaged(IntPtr unmanaged)
    {
        return Marshal.PtrToStructure<PropVariant>(unmanaged);
    }

    public static nint ConvertToUnmanaged(PropVariant managed)
    {
        var obj = Marshal.GetIUnknownForObject(managed);
        return obj;
    }
}

[CustomMarshaller(typeof(PropVariant), MarshalMode.ManagedToUnmanagedIn, typeof(PROPVARIANTRefMarshaller))]
[CustomMarshaller(typeof(PropVariant), MarshalMode.UnmanagedToManagedIn, typeof(PROPVARIANTRefMarshaller))]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "No need")]
public static partial class PROPVARIANTRefMarshaller
{
    public static void Free(IntPtr managed)
    {
        // Marshal.FreeCoTaskMem(managed);
    }

    public static PropVariant ConvertToManaged(IntPtr unmanaged)
    {
        var obj = Marshal.GetObjectForIUnknown(unmanaged);
        return (PropVariant)obj;
    }

    public static nint ConvertToUnmanaged(PropVariant managed)
    {
        var obj = Marshal.GetIUnknownForObject(managed);
        return obj;
    }
}
