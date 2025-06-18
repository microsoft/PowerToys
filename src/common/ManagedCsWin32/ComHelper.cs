// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;

namespace ManagedCsWin32;

public static class ComHelper
{
    public static T CreateComInstance<T>(ref Guid clsID, ref Guid iID, CLSCTX rclsCtx)
    {
        var cw = new StrategyBasedComWrappers();

        var hr = Ole32.CoCreateInstance(ref clsID, IntPtr.Zero, rclsCtx, ref iID, out IntPtr comPtr);
        if (hr != 0)
        {
            throw new ArgumentException($"Failed to create {typeof(T).Name} instance. HR: {hr}");
        }

        if (comPtr == IntPtr.Zero)
        {
            throw new ArgumentException($"Failed to create {typeof(T).Name} instance. CoCreateInstance return null ptr.");
        }

        var comObject = cw.GetOrCreateObjectForComInstance(comPtr, CreateObjectFlags.None);

        Marshal.Release(comPtr);
        if (comObject == null)
        {
            throw new ArgumentException($"Failed to create {typeof(T).Name} instance. Cast error.");
        }

        return (T)comObject;
    }
}
