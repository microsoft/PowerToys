// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Peek.Common.WIC
{
    [ComImport]
    [Guid(CLSID.WICImagingFactory)]
    [ComDefaultInterface(typeof(IWICImagingFactoryClass))]
    public class WICImagingFactory
    {
    }

    [ComImport]
    [Guid(IID.IWICImagingFactory)]
    [CoClass(typeof(WICImagingFactory))]
    public interface IWICImagingFactoryClass : IWICImagingFactory
    {
    }
}
