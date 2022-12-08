// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers.Helpers
{
    using System;
    using System.Drawing;
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

            if (hr != HResult.Ok)
            {
                // TODO: fallback to a generic icon
            }

            Marshal.ReleaseComObject(nativeShellItem);

            return hr;
        }
    }
}
