// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;

namespace Common
{
    /// <summary>
    /// Todo.
    /// </summary>
    public interface IPreviewHandlerControl
    {
        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="result">Rewsult.</param>
        void QueryFocus(out IntPtr result);

        /// <summary>
        /// Todo.
        /// </summary>
        void SetFocus();

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="font">font.</param>
        void SetFont(Font font);

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="color">color.</param>
        void SetTextColor(Color color);

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="argbColor">color.</param>
        void SetBackgroundColor(Color argbColor);

        /// <summary>
        /// Todo.
        /// </summary>
        /// <returns>pointrt.</returns>
        IntPtr GetHandle();

        /// <summary>
        /// Todo.
        /// </summary>
        void Unload();

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="windowBounds">Bounds.</param>
        void SetRect(Rectangle windowBounds);

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="hwnd">handle.</param>
        /// <param name="rect">Rectangle.</param>
        void SetWindow(IntPtr hwnd, Rectangle rect);
    }
}
