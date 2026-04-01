// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Peek.Common.WIC
{
    [GeneratedComInterface]
    [Guid(IID.IWICBitmapSource)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public partial interface IWICBitmapSource
    {
        void GetSize([Out] out int puiWidth, [Out] out int puiHeight);
    }
}
