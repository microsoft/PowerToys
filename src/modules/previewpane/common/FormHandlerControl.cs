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
    /// Todo.
    /// </summary>
    public class FormHandlerControl : UserControl, IPreviewHandlerControl
    {
        private IntPtr parentHwnd;

        /// <summary>
        /// Todo.
        /// </summary>
        /// <returns>Returns the handle of the control.</returns>
        public IntPtr GetHandle()
        {
            return this.Handle;
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="result">Todorsult.</param>
        public void QueryFocus(out IntPtr result)
        {
            var getResult = IntPtr.Zero;
            this.InvokeOnControlThread(() =>
            {
                getResult = GetFocus();
            });
            result = getResult;
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="argbColor">argbColor.</param>
        public void SetBackgroundColor(Color argbColor)
        {
            this.InvokeOnControlThread(() =>
            {
                this.BackColor = argbColor;
            });
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void SetFocus()
        {
            this.InvokeOnControlThread(() =>
            {
                this.Focus();
            });
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="font">font.</param>
        public void SetFont(Font font)
        {
            this.InvokeOnControlThread(() =>
            {
                this.Font = font;
            });
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="windowBounds">Bounds.</param>
        public void SetRect(Rectangle windowBounds)
        {
            this.UpdateWindowBounds(windowBounds);
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="color">color.</param>
        public void SetTextColor(Color color)
        {
            this.InvokeOnControlThread(() =>
            {
                this.ForeColor = color;
            });
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="hwnd">Handle.</param>
        /// <param name="rect">Rectangle.</param>
        public void SetWindow(IntPtr hwnd, Rectangle rect)
        {
            this.parentHwnd = hwnd;
            this.UpdateWindowBounds(rect);
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void Unload()
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

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetFocus();

        private void InvokeOnControlThread(MethodInvoker d)
        {
            this.Invoke(d);
        }

        private void UpdateWindowBounds(Rectangle windowBounds)
        {
            if (this.Visible)
            {
                this.InvokeOnControlThread(() =>
                {
                    SetParent(this.Handle, this.parentHwnd);
                    this.Bounds = windowBounds;
                    this.Visible = true;
                });
            }
        }
    }
}
