// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.Common.Models
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;

    public static class ShellApi
    {
        /// <summary>
        /// Gets the path to the given known folder.
        /// </summary>
        /// <param name="knownFolderId">Guid for given known folder</param>
        /// <returns>The path to the known folder.</returns>
        public static string GetKnownFolderPath(Guid knownFolderId)
        {
            string path;
            int hResult = SHGetKnownFolderPath(knownFolderId, 0, IntPtr.Zero, out path);
            Marshal.ThrowExceptionForHR(hResult);

            return path;
        }

        /// <summary>
        /// Gets a IPropertyStore interface from the given path.
        /// </summary>
        /// <param name="path">The file/folder path</param>
        /// <param name="flags">The property store flags</param>
        /// <returns>an IPropertyStroe interface</returns>
        public static IPropertyStore GetPropertyStoreFromPath(string path, PropertyStoreFlags flags = PropertyStoreFlags.EXTRINSICPROPERTIES)
        {
            ShellApi.IShellItem2? shellItem2 = null;
            IntPtr ppPropertyStore = IntPtr.Zero;

            try
            {
                ShellApi.SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(ShellApi.IShellItem2).GUID, out shellItem2);

                if (shellItem2 == null)
                {
                    throw new InvalidOperationException(string.Format("Unable to get an IShellItem2 reference from file {0}.", path));
                }

                int hr = shellItem2.GetPropertyStore((int)flags, typeof(ShellApi.IPropertyStore).GUID, out ppPropertyStore);

                if (hr != 0)
                {
                    throw new InvalidOperationException(string.Format("GetPropertyStore retunred hresult={0}", hr));
                }

                return (ShellApi.IPropertyStore)Marshal.GetObjectForIUnknown(ppPropertyStore);
            }
            finally
            {
                if (ppPropertyStore != IntPtr.Zero)
                {
                    Marshal.Release(ppPropertyStore);
                }

                if (shellItem2 != null)
                {
                    Marshal.ReleaseComObject(shellItem2);
                }
            }
        }

        /// <summary>
        /// Helper method that retrieves a uint value from the given property store.
        /// Returns 0 if the value is not a VT_UI4 (4-byte unsigned integer in little-endian order).
        /// </summary>
        /// <param name="propertyStore">The property store</param>
        /// <param name="key">The pkey</param>
        /// <returns>The uint value</returns>
        public static uint GetUIntFromPropertyStore(IPropertyStore propertyStore, PropertyKey key)
        {
            if (propertyStore == null)
            {
                throw new ArgumentNullException("propertyStore");
            }

            PropVariant propVar;

            propertyStore.GetValue(ref key, out propVar);

            // VT_UI4 Indicates a 4-byte unsigned integer formatted in little-endian byte order.
            if ((VarEnum)propVar.Vt == VarEnum.VT_UI4)
            {
                return propVar.UlVal;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Helper method that retrieves a ulong value from the given property store.
        /// Returns 0 if the value is not a VT_UI8 (8-byte unsigned integer in little-endian order).
        /// </summary>
        /// <param name="propertyStore">The property store</param>
        /// <param name="key">the pkey</param>
        /// <returns>the ulong value</returns>
        public static ulong GetULongFromPropertyStore(IPropertyStore propertyStore, PropertyKey key)
        {
            if (propertyStore == null)
            {
                throw new ArgumentNullException("propertyStore");
            }

            PropVariant propVar;

            propertyStore.GetValue(ref key, out propVar);

            // VT_UI8 Indicates an 8-byte unsigned integer formatted in little-endian byte order.
            if ((VarEnum)propVar.Vt == VarEnum.VT_UI8)
            {
                return propVar.UhVal;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Helper method that retrieves a string value from the given property store.
        /// </summary>
        /// <param name="propertyStore">The property store</param>
        /// <param name="key">The pkey</param>
        /// <returns>The string value</returns>
        public static string GetStringFromPropertyStore(IPropertyStore propertyStore, PropertyKey key)
        {
            PropVariant propVar;

            propertyStore.GetValue(ref key, out propVar);

            if ((VarEnum)propVar.Vt == VarEnum.VT_LPWSTR)
            {
                return Marshal.PtrToStringUni(propVar.P) ?? string.Empty;
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Helper method that retrieves an array of string values from the given property store.
        /// </summary>
        /// <param name="propertyStore">The property store</param>
        /// <param name="key">The pkey</param>
        /// <returns>The array of string values</returns>
        public static string[] GetStringArrayFromPropertyStore(IPropertyStore propertyStore, PropertyKey key)
        {
            PropVariant propVar;
            propertyStore.GetValue(ref key, out propVar);

            List<string> values = new List<string>();

            if ((VarEnum)propVar.Vt == (VarEnum.VT_LPWSTR | VarEnum.VT_VECTOR))
            {
                for (int elementIndex = 0; elementIndex < propVar.Calpwstr.CElems; elementIndex++)
                {
                    var stringVal = Marshal.PtrToStringUni(Marshal.ReadIntPtr(propVar.Calpwstr.PElems, elementIndex));
                    if (stringVal != null)
                    {
                        values.Add(stringVal);
                    }
                }
            }

            return values.ToArray();
        }

        private const string IIDIShellItem = "43826D1E-E718-42EE-BC55-A1E261C37BFE";
        private const string IIDIShellFolder2 = "93F2F68C-1D1B-11D3-A30E-00C04F79ABD1";
        private const string IIDIEnumIDList = "000214F2-0000-0000-C000-000000000046";
        private const string BHIDSFObject = "3981e224-f559-11d3-8e3a-00c04f6837d5";
        private const int KfFlagDefaultPath = 0x00000400;
        private const int SigdnNormalDisplay = 0;

        [Flags]
        public enum Shcontf : int
        {
            Folders = 0x0020,
            NonFolders = 0x0040,
            IncludeHidden = 0x0080,
            InitOnFirstNext = 0x0100,
            NetPrinterSearch = 0x0200,
            Shareable = 0x0400,
            Storage = 0x0800,
        }

        [SuppressMessage("Microsoft.Portability", "CA1900:ValueTypeFieldsShouldBePortable", Justification = "Targeting Windows (X86/AMD64/ARM) only")]
        [StructLayout(LayoutKind.Explicit)]
        public struct Strret
        {
            [FieldOffset(0)]
            public int UType;

            [FieldOffset(4)]
            public IntPtr POleStr;

            [FieldOffset(4)]
            public IntPtr PStr;

            [FieldOffset(4)]
            public int UOffset;

            [FieldOffset(4)]
            public IntPtr CStr;
        }

        public enum Shgno
        {
            Normal = 0x0000,
            InFolder = 0x0001,
            ForEditing = 0x1000,
            ForAddressBar = 0x4000,
            ForParsing = 0x8000,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHELLDETAILS
        {
            public int Fmt;
            public int CxChar;
            public Strret Str;
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid(IIDIShellFolder2)]
        public interface IShellFolder2
        {
            [PreserveSig]
            int ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, ref int pchEaten, out IntPtr ppidl, ref int pdwAttributes);

            [PreserveSig]
            int EnumObjects(IntPtr hwnd, Shcontf grfFlags, out IntPtr enumIDList);

            [PreserveSig]
            int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

            [PreserveSig]
            int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

            [PreserveSig]
            int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

            [PreserveSig]
            int CreateViewObject(IntPtr hwndOwner, Guid riid, out IntPtr ppv);

            [PreserveSig]
            int GetAttributesOf(int cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref IntPtr rgfInOut);

            [PreserveSig]
            int GetUIObjectOf(IntPtr hwndOwner, int cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);

            [PreserveSig]
            int GetDisplayNameOf(IntPtr pidl, Shgno uFlags, out Strret lpName);

            [PreserveSig]
            int SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, int uFlags, out IntPtr ppidlOut);

            [PreserveSig]
            int EnumSearches(out IntPtr ppenum);

            [PreserveSig]
            int GetDefaultColumn(int dwReserved, ref IntPtr pSort, out IntPtr pDisplay);

            [PreserveSig]
            int GetDefaultColumnState(int iColumn, out IntPtr pcsFlags);

            [PreserveSig]
            int GetDefaultSearchGUID(out IntPtr guid);

            [PreserveSig]
            int GetDetailsEx(IntPtr pidl, IntPtr pscid, out IntPtr pv);

            [PreserveSig]
            int GetDetailsOf(IntPtr pidl, int iColumn, ref SHELLDETAILS psd);

            [PreserveSig]
            int MapColumnToSCID(int icolumn, IntPtr pscid);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid(IIDIEnumIDList)]
        public interface IEnumIDList
        {
            [PreserveSig]
            int Next(int celt, out IntPtr rgelt, out int pceltFetched);

            [PreserveSig]
            int Skip(int celt);

            [PreserveSig]
            int Reset();

            [PreserveSig]
            int Clone(out IEnumIDList ppenum);
        }

        [ComImport]
        [Guid(IIDIShellItem)]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellItem
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);

            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            int GetDisplayName([In] int sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);

            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void GetAttributes([In] int sfgaoMask, out int psfgaoAttribs);

            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            void Compare([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In] uint hint, out int piOrder);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHFILEINFO
        {
            public IntPtr HIcon;
            public int IIcon;
            public uint DwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string SzDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string SzTypeName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Blob
        {
            public int CbSize;
            public IntPtr PBlobData;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct PropVariant
        {
            [FieldOffset(0)]
            public short Vt;
            [FieldOffset(2)]
            public short WReserved1;
            [FieldOffset(4)]
            public short WReserved2;
            [FieldOffset(6)]
            public short WReserved3;
            [FieldOffset(8)]
            public sbyte CVal;
            [FieldOffset(8)]
            public byte BVal;
            [FieldOffset(8)]
            public short IVal;
            [FieldOffset(8)]
            public ushort UiVal;
            [FieldOffset(8)]
            public int LVal;
            [FieldOffset(8)]
            public uint UlVal;
            [FieldOffset(8)]
            public int IntVal;
            [FieldOffset(8)]
            public uint UintVal;
            [FieldOffset(8)]
            public long HVal;
            [FieldOffset(8)]
            public ulong UhVal;
            [FieldOffset(8)]
            public float FltVal;
            [FieldOffset(8)]
            public double DblVal;
            [FieldOffset(8)]
            public bool BoolVal;
            [FieldOffset(8)]
            public int Scode;
            [FieldOffset(8)]
            public DateTime Date;
            [FieldOffset(8)]
            public FileTime Filetime;
            [FieldOffset(8)]
            public Blob Blob;
            [FieldOffset(8)]
            public IntPtr P;
            [FieldOffset(8)]
            public CALPWSTR Calpwstr;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CALPWSTR
        {
            public uint CElems;
            public IntPtr PElems;
        }

        public enum SIGDN : uint
        {
            NORMALDISPLAY = 0,
            PARENTRELATIVEPARSING = 0x80018001,
            PARENTRELATIVEFORADDRESSBAR = 0x8001c001,
            DESKTOPABSOLUTEPARSING = 0x80028000,
            PARENTRELATIVEEDITING = 0x80031001,
            DESKTOPABSOLUTEEDITING = 0x8004c000,
            FILESYSPATH = 0x80058000,
            URL = 0x80068000,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEMTIME
        {
            [MarshalAs(UnmanagedType.U2)]
            public short Year;
            [MarshalAs(UnmanagedType.U2)]
            public short Month;
            [MarshalAs(UnmanagedType.U2)]
            public short DayOfWeek;
            [MarshalAs(UnmanagedType.U2)]
            public short Day;
            [MarshalAs(UnmanagedType.U2)]
            public short Hour;
            [MarshalAs(UnmanagedType.U2)]
            public short Minute;
            [MarshalAs(UnmanagedType.U2)]
            public short Second;
            [MarshalAs(UnmanagedType.U2)]
            public short Milliseconds;

            public SYSTEMTIME(DateTime dt)
            {
                dt = dt.ToUniversalTime();  // SetSystemTime expects the SYSTEMTIME in UTC
                Year = (short)dt.Year;
                Month = (short)dt.Month;
                DayOfWeek = (short)dt.DayOfWeek;
                Day = (short)dt.Day;
                Hour = (short)dt.Hour;
                Minute = (short)dt.Minute;
                Second = (short)dt.Second;
                Milliseconds = (short)dt.Millisecond;
            }
        }

        [ComImport]
        [Guid("7E9FB0D3-919F-4307-AB2E-9B1860310C93")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellItem2 : IShellItem
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            new void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);

            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            new void GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            new int GetDisplayName([In] int sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);

            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            new void GetAttributes([In] int sfgaoMask, out int psfgaoAttribs);

            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            new void Compare([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In] uint hint, out int piOrder);

            [PreserveSig]
            int GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);

            [PreserveSig]
            int GetPropertyStoreWithCreateObject(ref PropertyStoreFlags flags, ref IntPtr punkFactory, ref Guid riid, out IntPtr ppv);

            [PreserveSig]
            int GetPropertyStoreForKeys(ref PropertyKey keys, uint cKeys, ref PropertyStoreFlags flags, ref Guid riid, out IntPtr ppv);

            [PreserveSig]
            int GetPropertyDescriptionList(ref PropertyKey key, ref Guid riid, out IntPtr ppv);

            [PreserveSig]
            int Update(ref IntPtr pbc);

            [PreserveSig]
            int GetProperty(ref PropertyKey key, out PropVariant pPropVar);

            [PreserveSig]
            int GetCLSID(ref PropertyKey key, out Guid clsid);

            [PreserveSig]
            int GetFileTime(ref PropertyKey key, out FileTime pft);

            [PreserveSig]
            int GetInt32(ref PropertyKey key, out int pi);

            [PreserveSig]
            int GetString(ref PropertyKey key, [MarshalAs(UnmanagedType.LPWStr)] string ppsz);

            [PreserveSig]
            int GetUint32(ref PropertyKey key, out uint pui);

            [PreserveSig]
            int GetUint64(ref PropertyKey key, out uint pull);

            [PreserveSig]
            int GetBool(ref PropertyKey key, bool pf);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FileTime
        {
            public int DWHighDateTime;
            public int DWLowDateTime;
        }

        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPropertyStore
        {
            void GetCount(out uint propertyCount);

            void GetAt(uint iProp, out PropertyKey pkey);

            void GetValue(ref PropertyKey key, out PropVariant pv);

            void SetValue(ref PropertyKey key, ref PropVariant pv);

            void Commit();
        }

        public enum PropertyStoreFlags
        {
            DEFAULT = 0x00000000,
            HANDLERPROPERTIESONLY = 0x00000001,
            READWRITE = 0x00000002,
            TEMPORARY = 0x00000004,
            FASTPROPERTIESONLY = 0x00000008,
            OPENSLOWITEM = 0x00000010,
            DELAYCREATION = 0x00000020,
            BESTEFFORT = 0x00000040,
            NO_OPLOCK = 0x00000080,
            PREFERQUERYPROPERTIES = 0x00000100,
            EXTRINSICPROPERTIES = 0x00000200,
            EXTRINSICPROPERTIESONLY = 0x00000400,
            MASK_VALID = 0x000007ff,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct PropertyKey
        {
            public Guid FormatId;
            public int PropertyId;

            public PropertyKey(Guid guid, int propertyId)
            {
                this.FormatId = guid;
                this.PropertyId = propertyId;
            }

            public PropertyKey(uint a, uint b, uint c, uint d, uint e, uint f, uint g, uint h, uint i, uint j, uint k, int propertyId)
                : this(new Guid((uint)a, (ushort)b, (ushort)c, (byte)d, (byte)e, (byte)f, (byte)g, (byte)h, (byte)i, (byte)j, (byte)k), propertyId)
            {
            }

            public override bool Equals(object? obj)
            {
                if ((obj == null) || !(obj is PropertyKey))
                {
                    return false;
                }

                PropertyKey pk = (PropertyKey)obj;

                return FormatId.Equals(pk.FormatId) && (PropertyId == pk.PropertyId);
            }

            public static bool operator ==(PropertyKey a, PropertyKey b)
            {
                if (((object)a == null) || ((object)b == null))
                {
                    return false;
                }

                return a.FormatId == b.FormatId && a.PropertyId == b.PropertyId;
            }

            public static bool operator !=(PropertyKey a, PropertyKey b)
            {
                return !(a == b);
            }

            public override int GetHashCode()
            {
                return FormatId.GetHashCode() ^ PropertyId;
            }
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrRetToBuf(ref Strret pstr, IntPtr pidl, StringBuilder pszBuf, int cchBuf);

        /// <summary>
        /// Extracts a specified string from a specified resource via an indirect string
        /// </summary>
        /// <param name="pszSource">An indirect string representing the desired string from a specified resource file</param>
        /// <param name="pszOutBuf">An output string, which receives the native function's outputted resource string</param>
        /// <param name="cchOutBuf">The buffer size to hold the output string, in characters</param>
        /// <param name="ppvReserved">A reserved pointer (void**)</param>
        /// <returns>Returns an HRESULT representing the success/failure of the native function</returns>
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        internal static extern uint SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, uint cchOutBuf, IntPtr ppvReserved);

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out string pszPath);

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderItem([In] ref Guid rfid, [In] int dwFlags, [In] IntPtr hToken, [In] ref Guid riid, [Out] out IntPtr ppv);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int SHGetPropertyStoreFromParsingName(
                string pszPath,
                IntPtr zeroWorks,
                PropertyStoreFlags flags,
                ref Guid iIdPropStore,
                [Out] out IPropertyStore propertyStore);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            [In] IntPtr pbc,
            [In][MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItem2 ppv);
    }
}
