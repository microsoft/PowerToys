// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ImageResizer.Utilities
{
    internal static class ShellPropertyStoreHelper
    {
        public static bool TryHasProperty(string path, string canonicalPropertyName)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(canonicalPropertyName))
            {
                return false;
            }

            using var propertyStore = GetPropertyStore(path, GetPropertyStoreFlags.GpsDefault | GetPropertyStoreFlags.GpsBestEffort);
            if (PSGetPropertyKeyFromName(canonicalPropertyName, out var propertyKey) != 0)
            {
                return false;
            }

            PropVariant value = default;
            try
            {
                propertyStore.PropertyStore.GetValue(ref propertyKey, out value);
                return value.Vt != 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to read shell property '{canonicalPropertyName}' from '{path}': {ex}");
                return false;
            }
            finally
            {
                _ = PropVariantClear(ref value);
            }
        }

        private static DisposablePropertyStore GetPropertyStore(string path, GetPropertyStoreFlags flags)
        {
            IShellItem2 shellItem = null;
            IntPtr propertyStorePointer = IntPtr.Zero;

            try
            {
                SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItem2).GUID, out shellItem);
                if (shellItem == null)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to create a shell item for '{0}'.", path));
                }

                var propertyStoreGuid = typeof(IPropertyStore).GUID;
                int hr = shellItem.GetPropertyStore((int)flags, ref propertyStoreGuid, out propertyStorePointer);
                if (hr != 0)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "GetPropertyStore failed for '{0}' with HRESULT 0x{1:X8}.", path, hr));
                }

                return new DisposablePropertyStore((IPropertyStore)Marshal.GetObjectForIUnknown(propertyStorePointer));
            }
            finally
            {
                if (propertyStorePointer != IntPtr.Zero)
                {
                    Marshal.Release(propertyStorePointer);
                }

                if (shellItem != null)
                {
                    Marshal.ReleaseComObject(shellItem);
                }
            }
        }

        [DllImport("propsys.dll", CharSet = CharSet.Unicode)]
        private static extern int PSGetPropertyKeyFromName(string pszCanonicalName, out PropertyKey propkey);

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant pvar);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            [In] IntPtr pbc,
            [In][MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItem2 ppv);

        [Flags]
        private enum GetPropertyStoreFlags
        {
            GpsDefault = 0x00000000,
            GpsBestEffort = 0x00000040,
        }

        private sealed class DisposablePropertyStore : IDisposable
        {
            public DisposablePropertyStore(IPropertyStore propertyStore)
            {
                PropertyStore = propertyStore;
            }

            public IPropertyStore PropertyStore { get; }

            public void Dispose()
            {
                if (PropertyStore != null)
                {
                    Marshal.ReleaseComObject(PropertyStore);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PropertyKey
        {
            public Guid FormatId;
            public int PropertyId;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct PropVariant
        {
            [FieldOffset(0)]
            public ushort Vt;
            [FieldOffset(2)]
            public ushort WReserved1;
            [FieldOffset(4)]
            public ushort WReserved2;
            [FieldOffset(6)]
            public ushort WReserved3;
            [FieldOffset(8)]
            public IntPtr PointerValue;
            [FieldOffset(8)]
            public long LongValue;
            [FieldOffset(8)]
            public ulong ULongValue;
            [FieldOffset(8)]
            public double DoubleValue;
            [FieldOffset(8)]
            public FILETIME Filetime;
        }

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);

            void GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            int GetDisplayName([In] int sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);

            void GetAttributes([In] int sfgaoMask, out int psfgaoAttribs);

            void Compare([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In] uint hint, out int piOrder);
        }

        [ComImport]
        [Guid("7E9FB0D3-919F-4307-AB2E-9B1860310C93")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem2 : IShellItem
        {
            new void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);

            new void GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

            new int GetDisplayName([In] int sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);

            new void GetAttributes([In] int sfgaoMask, out int psfgaoAttribs);

            new void Compare([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In] uint hint, out int piOrder);

            [PreserveSig]
            int GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);
        }

        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            void GetCount(out uint propertyCount);

            void GetAt(uint iProp, out PropertyKey pkey);

            void GetValue(ref PropertyKey key, out PropVariant pv);

            void SetValue(ref PropertyKey key, ref PropVariant pv);

            void Commit();
        }
    }
}
