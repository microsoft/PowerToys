// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Common
{
    /// <summary>
    /// Form based implementation of <see cref="IPreviewHandlerControl"/>.
    /// </summary>
    public abstract class FormHandlerControl : Form, IPreviewHandlerControl
    {
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
            // More details: https://docs.microsoft.com/en-us/archive/msdn-magazine/2007/january/windows-vista-and-office-writing-your-own-preview-handlers.
            var forceCreation = this.Handle;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Visible = false;
        }

        /// <inheritdoc />
        public IntPtr GetHandle()
        {
            return this.Handle;
        }

        /// <inheritdoc />
        public void QueryFocus(out IntPtr result)
        {
            var getResult = IntPtr.Zero;
            this.InvokeOnControlThread(() =>
            {
                getResult = GetFocus();
            });
            result = getResult;
        }

        /// <inheritdoc />
        public void SetBackgroundColor(Color argbColor)
        {
            this.InvokeOnControlThread(() =>
            {
                this.BackColor = argbColor;
            });
        }

        /// <inheritdoc />
        public void SetFocus()
        {
            this.InvokeOnControlThread(() =>
            {
                this.Focus();
            });
        }

        /// <inheritdoc />
        public void SetFont(Font font)
        {
            this.InvokeOnControlThread(() =>
            {
                this.Font = font;
            });
        }

        /// <inheritdoc />
        public void SetRect(Rectangle windowBounds)
        {
            this.UpdateWindowBounds(windowBounds);
        }

        /// <inheritdoc />
        public void SetTextColor(Color color)
        {
            this.InvokeOnControlThread(() =>
            {
                this.ForeColor = color;
            });
        }

        /// <inheritdoc />
        public void SetWindow(IntPtr hwnd, Rectangle rect)
        {
            this.parentHwnd = hwnd;
            this.UpdateWindowBounds(rect);
        }

        /// <inheritdoc />
        public virtual void Unload()
        {
            this.InvokeOnControlThread(() =>
            {
                this.Visible = false;
                foreach (Control c in this.Controls)
                {
                    c.Dispose();
                }

                this.Controls.Clear();
            });
        }

        /// <inheritdoc />
        public virtual void DoPreview<T>(T dataSource)
        {
            this.Visible = true;
        }

        /// <summary>
        /// Executes the specified delegate on the thread that owns the control's underlying window handle.
        /// </summary>
        /// <param name="func">Delegate to run.</param>
        public void InvokeOnControlThread(MethodInvoker func)
        {
            this.Invoke(func);
        }

        /// <summary>
        /// Changes the parent window of the specified child window.
        /// </summary>
        /// <param name="hWndChild">A handle to the child window.</param>
        /// <param name="hWndNewParent">A handle to the new parent window.</param>
        /// <returns>If the function succeeds, the return value is a handle to the previous parent window and NULL in case of failure.</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        /// <summary>
        /// Retrieves the handle to the window that has the keyboard focus, if the window is attached to the calling thread's message queue.
        /// </summary>
        /// <returns>The return value is the handle to the window with the keyboard focus. If the calling thread's message queue does not have an associated window with the keyboard focus, the return value is NULL.</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetFocus();

        /// <summary>
        /// Update the Form Control window with the passed rectangle.
        /// </summary>
        /// <param name="windowBounds">An instance of rectangle.</param>
        private void UpdateWindowBounds(Rectangle windowBounds)
        {
            this.InvokeOnControlThread(() =>
            {
                SetParent(this.Handle, this.parentHwnd);
                this.Bounds = windowBounds;
            });
        }
    }
}
