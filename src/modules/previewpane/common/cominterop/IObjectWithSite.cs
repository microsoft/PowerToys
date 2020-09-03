// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Common.ComInterlop
{
    /// <summary>
    /// Provides a simplified way to support communication between an object and its site in the container.
    /// </summary>
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("fc4801a3-2ba9-11cf-a229-00aa003d7352")]
    public interface IObjectWithSite
    {
        /// <summary>
        /// Provides the site's pointer to the site object.
        /// </summary>
        /// <param name="pUnkSite">Address of an interface pointer to the site managing this object.</param>
        void SetSite([In, MarshalAs(UnmanagedType.IUnknown)] object pUnkSite);

        /// <summary>
        /// Retrieves the last site set using the <see cref="SetSite(object)" /> method. In cases where there is no known site, the object returns an exception.
        /// </summary>
        /// <param name="riid">Provides the IID of the interface pointer returned in the <paramref name="ppvSite"/> parameter.</param>
        /// <param name="ppvSite">The address of the caller's void variable in which the object stores the interface pointer of the site last seen in the <see cref="SetSite(object)" />.</param>
        void GetSite(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppvSite);
    }
}
