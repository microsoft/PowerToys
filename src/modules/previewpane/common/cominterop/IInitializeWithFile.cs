// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Common.Cominterop
{
    /// <summary>
    /// Exposes a method to initialize a handler, such as a property handler, thumbnail handler, or preview handler, with a file path.
    /// </summary>
    [GeneratedComInterface]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("b7d14566-0509-4cce-a71f-0a554233bd9b")]
    public partial interface IInitializeWithFile
    {
        /// <summary>
        /// Initializes a handler with a file path.
        /// </summary>
        /// <param name="pszFilePath">File Path.</param>
        /// <param name="grfMode">Indicate the Access Mode either STGM_READ (Read Only Access) or STGM_READWRITE (Read and Write Access).</param>
        void Initialize([MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, uint grfMode);
    }
}
