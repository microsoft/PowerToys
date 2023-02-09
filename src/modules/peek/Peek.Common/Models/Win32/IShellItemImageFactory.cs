// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Peek.Common.Models
{
    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItemImageFactory
    {
        [PreserveSig]
        HResult GetImage(
        [In, MarshalAs(UnmanagedType.Struct)] NativeSize size,
        [In] ThumbnailOptions flags,
        [Out] out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeSize
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

    [Flags]
    public enum ThumbnailOptions
    {
        ResizeToFit = 0x00,
        BiggerSizeOk = 0x01,
        InMemoryOnly = 0x02,
        IconOnly = 0x04,
        ThumbnailOnly = 0x08,
        InCacheOnly = 0x10,
        ScaleUp = 0x100,
    }
}
