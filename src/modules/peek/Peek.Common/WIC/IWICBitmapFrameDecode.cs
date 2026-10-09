// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Peek.Common.WIC
{
    [ComImport]
    [Guid(IID.IWICBitmapFrameDecode)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IWICBitmapFrameDecode : IWICBitmapSource
    {
        new void GetSize([Out] out int puiWidth, [Out] out int puiHeight);
    }
}
