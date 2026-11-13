// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Microsoft.Workspaces.UITests
{
    // UIA bounds are physical pixels even when VSTest hosts the assembly in a DPI-unaware process.
    // Keep native coordinate operations in that same space, and restore the host thread afterward.
    // This scope must remain synchronous: a thread DPI context does not flow across await.
    internal sealed class PhysicalCoordinateScope : IDisposable
    {
        private readonly IntPtr previousContext;
        private bool disposed;

        internal PhysicalCoordinateScope()
        {
            previousContext = SetThreadDpiAwarenessContext(new IntPtr(-4)); // PER_MONITOR_AWARE_V2
            if (previousContext == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enter the physical-coordinate DPI scope.");
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                if (SetThreadDpiAwarenessContext(previousContext) == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not restore the test thread's DPI context.");
                }

                disposed = true;
            }

            GC.SuppressFinalize(this);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
    }
}
