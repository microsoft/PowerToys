// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace PeekUI.Helpers
{
    [Flags]
    public enum ThumbnailOptions
    {
        None = 0x00,
        BiggerSizeOk = 0x01,
        InMemoryOnly = 0x02,
        IconOnly = 0x04,
        ThumbnailOnly = 0x08,
        InCacheOnly = 0x10,
        ScaleUp = 0x100
    }

    public static class WindowsThumbnailProvider
    {
        private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
        public static readonly string ProgramDirectory = Directory.GetParent(Assembly.Location)!.ToString();
        public static readonly string ErrorIcon = Path.Combine(ProgramDirectory, "Assets", "e3f7-64.png");

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

        public static BitmapSource GetIcon(string fileName)
        {
            IntPtr hbitmap;
            HResult hr = GetIconImpl(Path.GetFullPath(fileName), out hbitmap);

            if (hr != HResult.Ok)
            {
                return new BitmapImage(new Uri(ErrorIcon));
            }

            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                // delete HBitmap to avoid memory leaks
                InteropHelper.DeleteObject(hbitmap);
            }

        }

        public static BitmapSource GetThumbnail(string fileName)
        {
            IntPtr hbitmap;
            HResult hr = GetThumbnailImpl(Path.GetFullPath(fileName), out hbitmap);

            if (hr != HResult.Ok)
            {
                return GetIcon(fileName);
            }

            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                // delete HBitmap to avoid memory leaks
                InteropHelper.DeleteObject(hbitmap);
            }
        }

        private static HResult GetIconImpl(string filename, out IntPtr hbitmap)
        {
            Guid shellItem2Guid = new Guid(IShellItem2Guid);
            int retCode = InteropHelper.SHCreateItemFromParsingName(filename, IntPtr.Zero, ref shellItem2Guid, out InteropHelper.IShellItem nativeShellItem);

            if (retCode != 0)
            {
                throw Marshal.GetExceptionForHR(retCode)!;
            }

            NativeSize Large = new NativeSize { Width = 256, Height = 256 };
            var options = ThumbnailOptions.BiggerSizeOk | ThumbnailOptions.IconOnly;

            HResult hr = ((IShellItemImageFactory)nativeShellItem).GetImage(Large, options, out hbitmap);

            Marshal.ReleaseComObject(nativeShellItem);

            return hr;
        }

        private static HResult GetThumbnailImpl(string filename, out IntPtr hbitmap)
        {
            Guid shellItem2Guid = new Guid(IShellItem2Guid);
            int retCode = InteropHelper.SHCreateItemFromParsingName(filename, IntPtr.Zero, ref shellItem2Guid, out InteropHelper.IShellItem nativeShellItem);

            if (retCode != 0)
            {
                throw Marshal.GetExceptionForHR(retCode)!;
            }

            NativeSize ExtraLarge = new NativeSize { Width = 1024, Height = 1024, };
            NativeSize Large = new NativeSize { Width = 256, Height = 256 };
            NativeSize Medium = new NativeSize { Width = 96, Height = 96 };
            NativeSize Small = new NativeSize { Width = 32, Height = 32 };

            var options = ThumbnailOptions.BiggerSizeOk | ThumbnailOptions.ThumbnailOnly | ThumbnailOptions.ScaleUp;

            HResult hr = ((IShellItemImageFactory)nativeShellItem).GetImage(ExtraLarge, options, out hbitmap);

            if (hr != HResult.Ok)
            {
                hr = ((IShellItemImageFactory)nativeShellItem).GetImage(Large, options, out hbitmap);
            }

            if (hr != HResult.Ok)
            {
                hr = ((IShellItemImageFactory)nativeShellItem).GetImage(Medium, options, out hbitmap);
            }

            if (hr != HResult.Ok)
            {
                hr = ((IShellItemImageFactory)nativeShellItem).GetImage(Small, options, out hbitmap);
            }

            Marshal.ReleaseComObject(nativeShellItem);

            return hr;
        }
    }
}
