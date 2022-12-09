// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.WindowEventHook
{
    using System;
    using System.Runtime.ConstrainedExecution;
    using Microsoft.Win32.SafeHandles;
    using Peek.UI.Native;

    public class WindowEventSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private WindowEventSafeHandle(IntPtr handle)
            : base(true)
        {
            SetHandle(handle);
        }

        public WindowEventSafeHandle()
            : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            NativeMethods.DeleteObject(this);
            return true;
        }
    }
}
