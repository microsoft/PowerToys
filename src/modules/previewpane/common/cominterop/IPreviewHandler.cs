// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Common.ComInterlop
{
    /// <summary>
    /// Exposes methods for the display of rich previews.
    /// </summary>
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("8895b1c6-b41f-4c1c-a562-0d564250836f")]
    public interface IPreviewHandler
    {
        /// <summary>
        /// Sets the parent window of the previewer window, as well as the area within the parent to be used for the previewer window.
        /// </summary>
        /// <param name="hwnd">A handle to the parent window.</param>
        /// <param name="rect">A pointer to a <see cref="RECT"/> defining the area for the previewer.</param>
        void SetWindow(IntPtr hwnd, ref RECT rect);

        /// <summary>
        /// Directs the preview handler to change the area within the parent hwnd that it draws into.
        /// </summary>
        /// <param name="rect">A pointer to a <see cref="RECT"/> to be used for the preview.</param>
        void SetRect(ref RECT rect);

        /// <summary>
        /// Directs the preview handler to load data from the source specified in an earlier Initialize method call, and to begin rendering to the previewer window.
        /// </summary>
        void DoPreview();

        /// <summary>
        /// Directs the preview handler to cease rendering a preview and to release all resources that have been allocated based on the item passed in during the initialization.
        /// </summary>
        void Unload();

        /// <summary>
        /// Directs the preview handler to set focus to itself.
        /// </summary>
        void SetFocus();

        /// <summary>
        /// Directs the preview handler to return the HWND from calling the GetFocus Function.
        /// </summary>
        /// <param name="phwnd">When this method returns, contains a pointer to the HWND returned from calling the GetFocus Function from the preview handler's foreground thread.</param>
        void QueryFocus(out IntPtr phwnd);

        /// <summary>
        /// Directs the preview handler to handle a keystroke passed up from the message pump of the process in which the preview handler is running.
        /// </summary>
        /// <param name="pmsg">A pointer to a window message.</param>
        /// <returns>If the keystroke message can be processed by the preview handler, the handler will process it and return S_OK(0). If the preview handler cannot process the keystroke message, it
        /// will offer it to the host using <see cref="IPreviewHandlerFrame.TranslateAccelerator(ref MSG)"/>. If the host processes the message, this method will return S_OK(0). If the host does not process the message, this method will return S_FALSE(1).
        /// </returns>
        [PreserveSig]
        uint TranslateAccelerator(ref MSG pmsg);
    }
}
