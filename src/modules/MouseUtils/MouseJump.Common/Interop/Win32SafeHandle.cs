// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace MouseJump.Common.Interop;

public sealed class Win32SafeHandle : SafeHandle
{
    public Win32SafeHandle(nint handle)
        : base(IntPtr.Zero, false)
    {
        this.SetHandle(handle);
    }

    public Win32SafeHandle(nint handle, bool ownsHandle = false)
        : base(IntPtr.Zero, ownsHandle)
    {
        this.SetHandle(handle);
    }

    public Win32SafeHandle(nint handle, bool ownsHandle, Func<IntPtr, bool> release)
        : base(IntPtr.Zero, ownsHandle)
    {
        this.ReleaseDelegate = release ?? throw new ArgumentNullException(nameof(release));
        this.SetHandle(handle);
    }

    private Func<IntPtr, bool>? ReleaseDelegate
    {
        get;
    }

    public override bool IsInvalid
        => handle == nint.Zero;

    protected override bool ReleaseHandle()
    {
        return this.ReleaseDelegate?.Invoke(this.handle) ?? true;
    }
}
