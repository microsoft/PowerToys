// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Wox.Infrastructure.Image
{
    [Flags]
    public enum ThumbnailOptions
    {
        RESIZETOFIT = 0x00,
        BiggerSizeOk = 0x01,
        InMemoryOnly = 0x02,
        IconOnly = 0x04,
        ThumbnailOnly = 0x08,
        InCacheOnly = 0x10,
    }

    public static class WindowsThumbnailProvider
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;

        // Based on https://stackoverflow.com/questions/21751747/extract-thumbnail-for-any-file-in-windows
        private const string IShellItem2Guid = "7E9FB0D3-919F-4307-AB2E-9B1860310C93";

        internal enum HResult
        {
            Ok = 0x0000,
            False = 0x0001,
            InvalidArguments = unchecked((int)0x80070057),
            OutOfMemory = unchecked((int)0x8007000E),
            NoInterface = unchecked((int)0x80004002),
            Fail = unchecked((int)0x80004005),
            ExtractionFailed = unchecked((int)0x8004B200),
            ElementNotFound = unchecked((int)0x80070490),
            TypeElementNotFound = unchecked((int)0x8002802B),
            NoObject = unchecked((int)0x800401E5),
            Win32ErrorCanceled = 1223,
            Canceled = unchecked((int)0x800704C7),
            ResourceInUse = unchecked((int)0x800700AA),
            AccessDenied = unchecked((int)0x80030005),
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellItemImageFactory
        {
            [PreserveSig]
            HResult GetImage(
            [In, MarshalAs(UnmanagedType.Struct)] NativeSize size,
            [In] ThumbnailOptions flags,
            [Out] out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NativeSize
        {
            private int width;
            private int height;

            public int Width
            {
                set { width = value; }
            }

            public int Height
            {
                set { height = value; }
            }
        }

        public static BitmapSource GetThumbnail(string fileName, int width, int height, ThumbnailOptions options)
        {
            IntPtr hBitmap = GetHBitmap(Path.GetFullPath(fileName), width, height, options);

            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                // delete HBitmap to avoid memory leaks
                NativeMethods.DeleteObject(hBitmap);
            }
        }

        private static IntPtr GetHBitmap(string fileName, int width, int height, ThumbnailOptions options)
        {
            Guid shellItem2Guid = new Guid(IShellItem2Guid);
            int retCode = NativeMethods.SHCreateItemFromParsingName(fileName, IntPtr.Zero, ref shellItem2Guid, out IShellItem nativeShellItem);

            if (retCode != 0)
            {
                throw Marshal.GetExceptionForHR(retCode);
            }

            NativeSize nativeSize = new NativeSize
            {
                Width = width,
                Height = height,
            };

            HResult hr = ((IShellItemImageFactory)nativeShellItem).GetImage(nativeSize, options, out IntPtr hBitmap);

            // if extracting image thumbnail and failed, extract shell icon
            if (options == ThumbnailOptions.ThumbnailOnly && hr == HResult.ExtractionFailed)
            {
                hr = ((IShellItemImageFactory)nativeShellItem).GetImage(nativeSize, ThumbnailOptions.IconOnly, out hBitmap);
            }

            Marshal.ReleaseComObject(nativeShellItem);

            if (hr == HResult.Ok)
            {
                return hBitmap;
            }

            throw new InvalidComObjectException($"Error while extracting thumbnail for {fileName}", Marshal.GetExceptionForHR((int)hr));
        }

        // We have to evaluate this in real time to not crash, if the user switches to Acrobat after starting PT Run.
        public static bool DoesPdfUseAcrobatAsProvider()
        {
            // First check of there is an provider other than Adobe. For example PowerToys.
            // Generic Guids used by Explorer to identify the configured provider types: {BB2E617C-0920-11d1-9A0B-00C04FC2D6C1} = Image thumbnail; {E357FCCD-A995-4576-B01F-234630154E96} = File thumbnail;
            RegistryKey key1 = Registry.ClassesRoot.OpenSubKey(".pdf\\shellex\\{E357FCCD-A995-4576-B01F-234630154E96}", false);
            string value1 = (string)key1?.GetValue(string.Empty);
            key1?.Close();
            RegistryKey key2 = Registry.ClassesRoot.OpenSubKey(".pdf\\shellex\\{BB2E617C-0920-11d1-9A0B-00C04FC2D6C1}", false);
            string value2 = (string)key2?.GetValue(string.Empty);
            key2?.Close();
            if (!string.IsNullOrEmpty(value1) || !string.IsNullOrEmpty(value2))
            {
                return false;
            }

            // Second check if Adobe is the default application.
            RegistryKey pdfKey = Registry.ClassesRoot.OpenSubKey(".pdf", false);
            string pdfApp = (string)pdfKey?.GetValue(string.Empty);
            pdfKey?.Close();
            if (string.IsNullOrEmpty(pdfApp) || !pdfApp.StartsWith("Acrobat.Document.", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check if the thumbnail handler from Adobe is disabled.
            RegistryKey adobeAppKey = Registry.ClassesRoot.OpenSubKey(pdfApp + "\\shellex\\{BB2E617C-0920-11d1-9A0B-00C04FC2D6C1}", false);
            string adobeAppProvider = (string)adobeAppKey?.GetValue(string.Empty);
            adobeAppKey?.Close();
            if (string.IsNullOrEmpty(adobeAppProvider))
            {
                // No Adobe handler.
                return false;
            }

            // Thumbnail handler from Adobe is enabled and used.
            return true;
        }
    }
}
