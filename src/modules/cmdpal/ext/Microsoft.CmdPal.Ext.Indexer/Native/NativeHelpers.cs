// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

using VARTYPE = System.Runtime.InteropServices.VarEnum;

namespace Microsoft.CmdPal.Ext.Indexer.Native;

public sealed partial class NativeHelpers
{
    public const uint SEEMASKINVOKEIDLIST = 12;

    public struct PropertyKeys
    {
        public static readonly PropertyKey PKEYItemNameDisplay = new() { fmtid = new System.Guid("B725F130-47EF-101A-A5F1-02608C9EEBAC"), pid = 10 };
        public static readonly PropertyKey PKEYItemUrl = new() { fmtid = new System.Guid("49691C90-7E17-101A-A91C-08002B2ECDA9"), pid = 9 };
        public static readonly PropertyKey PKEYKindText = new() { fmtid = new System.Guid("F04BEF95-C585-4197-A2B7-DF46FDC9EE6D"), pid = 100 };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PropertyKey
    {
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "No, please do not change the name")]
        public Guid fmtid;

        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "No, please do not change the name")]
        public int pid;

        public PropertyKey(Guid fmtid, int pid)
        {
            this.fmtid = fmtid;
            this.pid = pid;
        }
    }

    [StructLayout(LayoutKind.Explicit, Pack = 8, CharSet = CharSet.Unicode)]
    public struct PROPVARIANT
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

        public PROPVARIANT()
        {
        }

        public PROPVARIANT(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            vt = (ushort)VarEnum.VT_LPWSTR;
            _ptr = Marshal.StringToCoTaskMemUni(value);
        }

        public VarEnum VarType { get => (VarEnum)vt; set => vt = (ushort)(VARTYPE)value; }
    }

    [CustomMarshaller(typeof(PROPVARIANT), MarshalMode.UnmanagedToManagedOut, typeof(PROPVARIANTOutMarshaller))]
    [CustomMarshaller(typeof(PROPVARIANT), MarshalMode.ManagedToUnmanagedOut, typeof(PROPVARIANTOutMarshaller))]
    public static partial class PROPVARIANTOutMarshaller
    {
        public static void Free(IntPtr managed)
        {
            // Marshal.FreeCoTaskMem(managed);
        }

        public static PROPVARIANT ConvertToManaged(IntPtr unmanaged)
        {
            var obj = Marshal.GetObjectForIUnknown(unmanaged);
            return (PROPVARIANT)obj;
        }

        public static nint ConvertToUnmanaged(PROPVARIANT managed)
        {
            var obj = Marshal.GetIUnknownForObject(managed);
            return obj;
        }
    }

    [CustomMarshaller(typeof(PROPVARIANT), MarshalMode.ManagedToUnmanagedRef, typeof(PROPVARIANTRefMarshaller))]
    [CustomMarshaller(typeof(PROPVARIANT), MarshalMode.UnmanagedToManagedRef, typeof(PROPVARIANTRefMarshaller))]
    public static partial class PROPVARIANTRefMarshaller
    {
        public static void Free(IntPtr managed)
        {
            // Marshal.FreeCoTaskMem(managed);
        }

        public static PROPVARIANT ConvertToManaged(IntPtr unmanaged)
        {
            var obj = Marshal.GetObjectForIUnknown(unmanaged);
            return (PROPVARIANT)obj;
        }

        public static nint ConvertToUnmanaged(PROPVARIANT managed)
        {
            var obj = Marshal.GetIUnknownForObject(managed);
            return obj;
        }
    }

    public static class OleDb
    {
        public static readonly Guid DbGuidDefault = new("C8B521FB-5CF3-11CE-ADE5-00AA0044773D");
    }
}
