// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace ShortcutGuide.Helpers
{
    [GeneratedComInterface]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    internal partial interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int GetAt(uint index, out NativeMethods.PROPERTYKEY key);

        [PreserveSig]
        int GetValue(in NativeMethods.PROPERTYKEY key, out NativeMethods.PROPVARIANT value);

        [PreserveSig]
        int SetValue(in NativeMethods.PROPERTYKEY key, in NativeMethods.PROPVARIANT value);

        [PreserveSig]
        int Commit();
    }
}
