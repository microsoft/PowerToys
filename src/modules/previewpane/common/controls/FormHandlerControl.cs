﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Windows.Forms;

using Common.ComInterlop;
using PreviewHandlerCommon.ComInterop;

namespace Common
{
    /// <summary>
    /// Form based implementation of <see cref="IPreviewHandlerControl"/>.
    /// </summary>
    public abstract class FormHandlerControl : Form, IPreviewHandlerControl
    {
        /// <summary>
        /// Needed to make the form into a child window.
        /// </summary>
        private static int gwlStyle = -16;
        private static int wsChild = 0x40000000;

        /// <summary>
        /// Holds the parent window handle.
        /// </summary>
        private IntPtr parentHwnd;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormHandlerControl"/> class.
        /// </summary>
        public FormHandlerControl()
        {
            // Gets the handle of the control to create the control on the VI thread. Invoking the Control.Handle get accessor forces the creation of the underlying window for the control.
            // This is important, because the thread that instantiates the preview handler component and calls its constructor is a single-threaded apartment (STA) thread, but the thread that calls into the interface members later on is a multithreaded apartment (MTA) thread. Windows Forms controls are meant to run on STA threads.
            // More details: https://learn.microsoft.com/archive/msdn-magazine/2007/january/windows-vista-and-office-writing-your-own-preview-handlers.
            var forceCreation = this.Handle;

            this.FormBorderStyle = FormBorderStyle.None;
            this.Visible = false;
        }

        /// <inheritdoc />
        public IntPtr GetWindowHandle()
        {
            return this.Handle;
        }

        /// <inheritdoc />
        public void QueryFocus(out IntPtr result)
        {
            var getResult = IntPtr.Zero;
            getResult = NativeMethods.GetFocus();
            result = getResult;
        }

        /// <inheritdoc />
        public void SetBackgroundColor(Color argbColor)
        {
            this.BackColor = argbColor;
        }

        /// <inheritdoc />
        public void SetFocus()
        {
            this.Focus();
        }

        /// <inheritdoc />
        public void SetFont(Font font)
        {
            this.Font = font;
        }

        /// <inheritdoc />
        public bool SetRect(Rectangle windowBounds)
        {
            return this.UpdateWindowBounds(parentHwnd, windowBounds);
        }

        /// <inheritdoc />
        public void SetTextColor(Color color)
        {
            this.ForeColor = color;
        }

        /// <inheritdoc />
        public bool SetWindow(IntPtr hwnd, Rectangle rect)
        {
            this.parentHwnd = hwnd;
            return this.UpdateWindowBounds(hwnd, rect);
        }

        /// <inheritdoc />
        public virtual void Unload()
        {
            this.Visible = false;
            foreach (Control c in this.Controls)
            {
                c.Dispose();
            }

            this.Controls.Clear();

            // Call garbage collection at the time of unloading of Preview.
            // Which is preventing prevhost.exe to exit at the time of closing File explorer.
            // Preview Handlers run in a separate process from PowerToys. This will not affect the performance of other modules.
            // Mitigate the following GitHub issue: https://github.com/microsoft/PowerToys/issues/1468
            GC.Collect();
        }

        /// <inheritdoc />
        public virtual void DoPreview<T>(T dataSource)
        {
            this.Visible = true;
        }

        /// <summary>
        /// Update the Form Control window with the passed rectangle.
        /// </summary>
        public bool UpdateWindowBounds(IntPtr hwnd, Rectangle newBounds)
        {
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
            {
                // If the HWND is IntPtr.Zero the desktop window will be used as parent.
                return false;
            }

            if (this.Disposing || this.IsDisposed)
            {
                // For unclear reasons, this can be called when handling an error and the form has already been disposed.
                return false;
            }

            // We must set the WS_CHILD style to change the form to a control within the Explorer preview pane
            int windowStyle = NativeMethods.GetWindowLong(Handle, gwlStyle);
            if ((windowStyle & wsChild) == 0)
            {
                _ = NativeMethods.SetWindowLong(Handle, gwlStyle, windowStyle | wsChild);
            }

            if (NativeMethods.SetParent(Handle, hwnd) == IntPtr.Zero)
            {
                return false;
            }

            if (newBounds.IsEmpty)
            {
                RECT s = default(RECT);
                NativeMethods.GetClientRect(hwnd, ref s);
                newBounds = new Rectangle(s.Left, s.Top, s.Right - s.Left, s.Bottom - s.Top);
            }

            if (Bounds.Right != newBounds.Right || Bounds.Bottom != newBounds.Bottom || Bounds.Left != newBounds.Left || Bounds.Top != newBounds.Top)
            {
                Bounds = newBounds;
            }

            return true;
        }
    }
}
