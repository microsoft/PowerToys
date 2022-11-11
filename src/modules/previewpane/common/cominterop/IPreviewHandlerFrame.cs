// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Common.ComInterlop
{
    /// <summary>
    /// Enables preview handlers to pass keyboard shortcuts to the host. This interface retrieves a list of keyboard shortcuts and directs the host to handle a keyboard shortcut.
    /// </summary>
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("fec87aaf-35f9-447a-adb7-20234491401a")]
    public interface IPreviewHandlerFrame
    {
        /// <summary>
        /// Gets a list of the keyboard shortcuts for the preview host.
        /// </summary>
        /// <param name="pinfo">A pointer to a <see href="https://learn.microsoft.com/windows/win32/api/shobjidl_core/ns-shobjidl_core-previewhandlerframeinfo">PREVIEWHANDLERFRAMEINFO</see> structure
        /// that receives accelerator table information.</param>
        void GetWindowContext(IntPtr pinfo);

        /// <summary>
        /// Directs the host to handle an keyboard shortcut passed from the preview handler.
        /// </summary>
        /// <param name="pmsg">A reference to <see cref="MSG"/> that corresponds to a keyboard shortcut.</param>
        /// <returns>If the keyboard shortcut is one that the host intends to handle, the host will process it and return S_OK(0); otherwise, it returns S_FALSE(1).</returns>
        [PreserveSig]
        uint TranslateAccelerator(ref MSG pmsg);
    }
}
