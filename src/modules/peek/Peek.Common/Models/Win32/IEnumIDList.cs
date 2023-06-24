// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Peek.Common.Models
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F2-0000-0000-C000-000000000046")]
    public interface IEnumIDList
    {
        [PreserveSig]
#pragma warning disable CA1716
        int Next(int celt, out IntPtr rgelt, out int pceltFetched);
#pragma warning restore CA1716

        [PreserveSig]
        int Skip(int celt);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Clone(out IEnumIDList ppenum);
    }
}
