// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Peek.Common.WIC
{
    [ComImport]
    [Guid(IID.IWICImagingFactory)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IWICImagingFactory
    {
        IWICBitmapDecoder CreateDecoderFromFilename(
            [In, MarshalAs(UnmanagedType.LPWStr)] string wzFilename,
            [In] IntPtr pguidVendor,
            [In] StreamAccessMode dwDesiredAccess,
            [In] WICDecodeOptions metadataOptions);
    }
}
