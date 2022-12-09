// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers.Helpers
{
    using System;
    using System.Runtime.InteropServices;
    using Peek.Common;
    using Peek.Common.Models;

    public static class IconHelper
    {
        // Based on https://stackoverflow.com/questions/21751747/extract-thumbnail-for-any-file-in-windows
        private const string IShellItem2Guid = "7E9FB0D3-919F-4307-AB2E-9B1860310C93";

        public static HResult GetIcon(string fileName, out IntPtr hbitmap)
        {
            Guid shellItem2Guid = new Guid(IShellItem2Guid);
            int retCode = NativeMethods.SHCreateItemFromParsingName(fileName, IntPtr.Zero, ref shellItem2Guid, out IShellItem nativeShellItem);

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

        // Based on https://stackoverflow.com/questions/24257506/how-can-i-get-messagebox-icons-in-windows-8-1
        // and https://stackoverflow.com/questions/28525925/get-icon-128128-file-type-c-sharp
        public static HResult GetSystemIcon(NativeMethods.SHSTOCKICONID systemIconId, out IntPtr hbitmap)
        {
            hbitmap = IntPtr.Zero;

            // Retrieve system stock icon's system index
            NativeMethods.SHSTOCKICONINFO sii = new ();
            sii.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.SHSTOCKICONINFO));
            HResult hr = NativeMethods.SHGetStockIconInfo(systemIconId, NativeMethods.SHGSI.SHGSI_SYSICONINDEX, ref sii);

            if (hr == HResult.Ok)
            {
                // Based on the index, retrieve the jumbo (256x256) version of the icon
                // I think any of the two ids works but putting both of them in case we need them in the future
                // const string IID_IImageList = "46EB5926-582E-4017-9FDF-E8998DAA0950";
                const string IID_IImageList2 = "192B9D83-50FC-457B-90A0-2B82A8B5DAE1";
                const int SHIL_JUMBO = 0x4; // 256x256
                const int ILD_TRANSPARENT = 0x00000001;
                const int ILD_IMAGE = 0x00000020;

                NativeMethods.IImageList? spiml = null;
                Guid guil = new Guid(IID_IImageList2);

                hr = NativeMethods.SHGetImageList(SHIL_JUMBO, ref guil, ref spiml!);
                spiml.GetIcon(sii.iSysIconIndex, ILD_TRANSPARENT | ILD_IMAGE, ref hbitmap);
            }

            return hr;
        }
    }
}
