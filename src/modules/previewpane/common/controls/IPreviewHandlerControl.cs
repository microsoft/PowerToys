// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;

namespace Common
{
    /// <summary>
    /// Interface defining preview handler control.
    /// </summary>
    public interface IPreviewHandlerControl
    {
        /// <summary>
        /// Directs the preview handler to return the HWND from calling the GetFocus function.
        /// Source: https://learn.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-ipreviewhandler-queryfocus.
        /// </summary>
        /// <param name="result">Returns the handle of the window with focus.</param>
        void QueryFocus(out IntPtr result);

        /// <summary>
        /// Sets focus to the control.
        /// </summary>
        void SetFocus();

        /// <summary>
        /// Sets the font according to the font set in Windows Settings.
        /// More details: https://learn.microsoft.com/windows/win32/shell/building-preview-handlers#ipreviewhandlervisualssetfont.
        /// </summary>
        /// <param name="font">Instance of Font.</param>
        void SetFont(Font font);

        /// <summary>
        /// Sets the Text color according to the Windows Settings.
        /// </summary>
        /// <param name="color">Instance of color.</param>
        void SetTextColor(Color color);

        /// <summary>
        /// Sets the Background color. For instance to fill the window when the handler renders to area smaller provided by SetWindow and SetRect.
        /// </summary>
        /// <param name="argbColor">Instance of color.</param>
        void SetBackgroundColor(Color argbColor);

        /// <summary>
        /// Gets the HWND of the control window.
        /// </summary>
        /// <returns>Pointer to the window handle.</returns>
        IntPtr GetWindowHandle();

        /// <summary>
        /// Hide the preview and free any resource used for the preview.
        /// </summary>
        void Unload();

        /// <summary>
        /// Directs the control to change the area within the parent hwnd that it draws into.
        /// </summary>
        /// <param name="windowBounds">Instance of Rectangle defining the area.</param>
        void SetRect(Rectangle windowBounds);

        /// <summary>
        /// Sets the parent window of the previewer window, as well as the area within the parent to be used for the previewer window..
        /// </summary>
        /// <param name="hwnd">Pointer to the parent window handle.</param>
        /// <param name="rect">Instance of Rectangle defining the area.</param>
        void SetWindow(IntPtr hwnd, Rectangle rect);

        /// <summary>
        /// Called by Preview Handler to start the preview.
        /// </summary>
        /// <typeparam name="T">File Path or Stream reference for the file.</typeparam>
        /// <param name="dataSource">Represents the source of preview data.</param>
        void DoPreview<T>(T dataSource);
    }
}
