// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

public static class ComFreeHelper
{
    internal static unsafe string GetStringAndFree(HRESULT hr, PWSTR ptr)
    {
        hr.ThrowOnFailure();
        try
        {
            return ptr.ToString();
        }
        finally
        {
            PInvoke.CoTaskMemFree(ptr);
        }
    }

    public static unsafe void ComObjectRelease<T>(T* comPtr)
        where T : unmanaged
    {
        if (comPtr is not null)
        {
            ((IUnknown*)comPtr)->Release();
        }
    }
}
