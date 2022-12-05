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
using Peek.UI.Models;
using Peek.UI.Native;
using static Peek.UI.Native.NativeModels;

namespace Peek.UI.Helpers
{
    public static class ThumbnailHelper
    {
        private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
        public static readonly string ProgramDirectory = Directory.GetParent(Assembly.Location)!.ToString();
        public static readonly string ErrorIcon = Path.Combine(ProgramDirectory, "Assets", "error.png");

        // Based on https://stackoverflow.com/questions/21751747/extract-thumbnail-for-any-file-in-windows
        private const string IShellItem2Guid = "7E9FB0D3-919F-4307-AB2E-9B1860310C93";

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
                NativeMethods.DeleteObject(hbitmap);
            }
        }

        public static BitmapSource GetThumbnail(string fileName, bool iconFallback)
        {
            IntPtr hbitmap;
            HResult hr = GetThumbnailImpl(Path.GetFullPath(fileName), out hbitmap);

            if (hr != HResult.Ok && iconFallback)
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
                NativeMethods.DeleteObject(hbitmap);
            }
        }

        private static HResult GetIconImpl(string filename, out IntPtr hbitmap)
        {
            Guid shellItem2Guid = new Guid(IShellItem2Guid);
            int retCode = NativeMethods.SHCreateItemFromParsingName(filename, IntPtr.Zero, ref shellItem2Guid, out IShellItem nativeShellItem);

            if (retCode != 0)
            {
                throw Marshal.GetExceptionForHR(retCode)!;
            }

            NativeSize large = new NativeSize { Width = 256, Height = 256 };
            var options = ThumbnailOptions.BiggerSizeOk | ThumbnailOptions.IconOnly;

            HResult hr = ((IShellItemImageFactory)nativeShellItem).GetImage(large, options, out hbitmap);

            Marshal.ReleaseComObject(nativeShellItem);

            return hr;
        }

        private static HResult GetThumbnailImpl(string filename, out IntPtr hbitmap)
        {
            Guid shellItem2Guid = new Guid(IShellItem2Guid);
            int retCode = NativeMethods.SHCreateItemFromParsingName(filename, IntPtr.Zero, ref shellItem2Guid, out IShellItem nativeShellItem);

            if (retCode != 0)
            {
                throw Marshal.GetExceptionForHR(retCode)!;
            }

            var extraLarge = new NativeSize { Width = 1024, Height = 1024, };
            var large = new NativeSize { Width = 256, Height = 256 };
            var medium = new NativeSize { Width = 96, Height = 96 };
            var small = new NativeSize { Width = 32, Height = 32 };

            var options = ThumbnailOptions.BiggerSizeOk | ThumbnailOptions.ThumbnailOnly | ThumbnailOptions.ScaleUp;

            HResult hr = ((IShellItemImageFactory)nativeShellItem).GetImage(extraLarge, options, out hbitmap);

            if (hr != HResult.Ok)
            {
                hr = ((IShellItemImageFactory)nativeShellItem).GetImage(large, options, out hbitmap);
            }

            if (hr != HResult.Ok)
            {
                hr = ((IShellItemImageFactory)nativeShellItem).GetImage(medium, options, out hbitmap);
            }

            if (hr != HResult.Ok)
            {
                hr = ((IShellItemImageFactory)nativeShellItem).GetImage(small, options, out hbitmap);
            }

            Marshal.ReleaseComObject(nativeShellItem);

            return hr;
        }
    }
}
