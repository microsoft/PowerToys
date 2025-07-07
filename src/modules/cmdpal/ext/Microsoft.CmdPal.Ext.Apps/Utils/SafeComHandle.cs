// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

public partial class SafeComHandle : SafeHandle
{
    public SafeComHandle()
        : base(IntPtr.Zero, ownsHandle: true)
    {
    }

    public SafeComHandle(IntPtr handle)
        : base(IntPtr.Zero, ownsHandle: true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        var count = Marshal.Release(handle);
        return true;
    }
}
