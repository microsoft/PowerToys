// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Peek.Common.Extensions;
using Peek.Common.Models;
using Windows.Foundation;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Peek.Common.Helpers
{
    public static partial class PropertyStoreHelper
    {
        /// <summary>
        /// Gets a ulong type value from PropertyStore from the given item.
        /// </summary>
        /// <param name="path">The file/folder path</param>
        /// <param name="key">The property key</param>
        /// <returns>a nullable ulong</returns>
        public static ulong? TryGetUlongProperty(string path, PropertyKey key)
        {
            using DisposablePropertyStore propertyStore = GetPropertyStoreFromPath(path);
            return propertyStore.TryGetULong(key);
        }

        /// <summary>
        /// Gets a string type value from PropertyStore from the given item.
        /// </summary>
        /// <param name="path">The file/folder path</param>
        /// <param name="key">The property key</param>
        /// <returns>a nullable string</returns>
        public static string? TryGetStringProperty(string path, PropertyKey key)
        {
            using DisposablePropertyStore propertyStore = GetPropertyStoreFromPath(path);
            return propertyStore.TryGetString(key);
        }

        /// <summary>
        /// Gets Size composed of weight (uint) and height (uint) from PropertyStore from the given item.
        /// </summary>
        /// <param name="path">The file/folder path</param>
        /// <param name="widthKey">The property key for width</param>
        /// <param name="heightKey">The property key for height</param>
        /// <returns>a nullable string</returns>
        public static Size? TryGetUintSizeProperty(string path, PropertyKey widthKey, PropertyKey heightKey)
        {
            Size? size = null;
            using DisposablePropertyStore propertyStore = GetPropertyStoreFromPath(path);
            uint? width = propertyStore.TryGetUInt(widthKey);
            uint? height = propertyStore.TryGetUInt(heightKey);

            if (width != null && height != null)
            {
                size = new Size((float)width, (float)height);
            }

            return size;
        }

        /// <summary>
        /// Gets a IPropertyStore interface (wrapped in DisposablePropertyStore) from the given path.
        /// </summary>
        /// <param name="path">The file/folder path</param>
        /// <param name="flags">The property store flags</param>
        /// <returns>an IPropertyStore interface</returns>
        private static DisposablePropertyStore GetPropertyStoreFromPath(string path, GETPROPERTYSTOREFLAGS flags = GETPROPERTYSTOREFLAGS.GPS_EXTRINSICPROPERTIES | GETPROPERTYSTOREFLAGS.GPS_BESTEFFORT)
        {
            IShellItem2? shellItem2 = null;
            IntPtr ppPropertyStore = IntPtr.Zero;

            try
            {
                SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItem2).GUID, out shellItem2);

                if (shellItem2 == null)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to get an IShellItem2 reference from file {0}.", path));
                }

                int hr = shellItem2.GetPropertyStore((int)flags, typeof(IPropertyStore).GUID, out ppPropertyStore);

                if (hr != 0)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "GetPropertyStore returned hresult={0}, errorMessage={1}", hr, Marshal.GetExceptionForHR(hr)!.Message));
                }

                return new DisposablePropertyStore((IPropertyStore)Marshal.GetObjectForIUnknown(ppPropertyStore));
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

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            [In] IntPtr pbc,
            [In][MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItem2 ppv);
    }
}
