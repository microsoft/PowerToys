// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MouseJump.Common.Interop;

/// <summary>
/// Wrapper around a WNDPROC delegate that can be handed to
/// external assemblies to manage native WNDPROC delegate lifetime
/// without exposing internals.
/// </summary>
public sealed class Win32WindowProc
{
    /// <summary>
    /// Public definition of WNDPROC using CLR types instead of CsWin32 generated types.
    /// </summary>
    public delegate nint WindowProcDelegate(nint hWnd, uint msg, nuint wParam, nint lParam);

    internal Win32WindowProc(WindowProcDelegate windowProc)
    {
        this.WindowProcInternal = windowProc ?? throw new ArgumentNullException(nameof(windowProc));

        // tl;dr...
        //
        // * Native code stores only the unmanaged function pointer.
        // * Managed code must keep the delegate alive for native code to call.
        //
        // long version...
        //
        // A *method group* (e.g. WndProcInternal) is *not* a delegate (WNDPROC).
        // When a method group is passed to anything that receives a delegate, the
        // CLR creates a new temporary delegate instance based on the method group.
        //
        // Windows api methods such as RegisterClassEx don't store a reference to
        // the managed delegate object - they only store an unmanaged function
        // pointer. Once the delegate instance becomes unreferenced on the managed
        // side the GC is allowed to collect it, even though the Win32 api continues
        // to store the unmanaged pointer to it. If Win32 subsequently calls the
        // (now invalid) unmanaged function pointer it can cause access violations
        // or other runtime crashes.
        //
        // To prevent this, we'll cache a strongly-typed reference to the managed
        // *delegate* instance in WndProcDelegate. Keeping this delegate reference
        // alive ensures the underlying unmanaged function pointer remains valid
        // for the entire lifetime of this Window instance.
        this.WndProcDelegate = this.WndProcInternal;
    }

    internal ushort ClassAtom
    {
        get;
    }

    private WindowProcDelegate WindowProcInternal
    {
        get;
    }

    internal WNDPROC WndProcDelegate
    {
        get;
    }

    private LRESULT WndProcInternal(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        return (LRESULT)this.WindowProcInternal(hWnd, msg, wParam, lParam);
    }
}
