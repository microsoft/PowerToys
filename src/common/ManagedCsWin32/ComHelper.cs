// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace ManagedCsWin32;

public static class ComHelper
{
    private static StrategyBasedComWrappers cw = new StrategyBasedComWrappers();

    public static T CreateComInstance<T>(ref Guid rclsid, CLSCTX dwClsContext)
    {
        var riid = typeof(T).GUID;

        var hr = Ole32.CoCreateInstance(ref rclsid, IntPtr.Zero, dwClsContext, ref riid, out IntPtr comPtr);
        if (hr != 0)
        {
            throw new ArgumentException($"Failed to create {typeof(T).Name} instance. HR: {hr}");
        }

        if (comPtr == IntPtr.Zero)
        {
            throw new ArgumentException($"Failed to create {typeof(T).Name} instance. CoCreateInstance return null ptr.");
        }

        try
        {
            var comObject = cw.GetOrCreateObjectForComInstance(comPtr, CreateObjectFlags.None);
            if (comObject == null)
            {
                throw new ArgumentException($"Failed to create {typeof(T).Name} instance. Cast error.");
            }

            return (T)comObject;
        }
        finally
        {
            Marshal.Release(comPtr);
        }
    }
}
