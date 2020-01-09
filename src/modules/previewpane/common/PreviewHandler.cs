// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using Common.ComInterlop;

namespace Common
{
    /// <summary>
    /// Todo.
    /// </summary>
    public abstract class PreviewHandler : IPreviewHandler, IOleWindow, IObjectWithSite, IPreviewHandlerVisuals
    {
        private IPreviewHandlerControl previewControl;
        private IntPtr parentHwnd;
        private Rectangle windowBounds;
        private object unkSite;
        private IPreviewHandlerFrame frame;

        /// <summary>
        /// Initializes a new instance of the <see cref="PreviewHandler"/> class.
        /// </summary>
        public PreviewHandler()
        {
            this.previewControl = this.CreatePreviewHandlerControl();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public abstract void DoPreview();

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="hwnd">hanlde.</param>
        /// <param name="rect">rectangle.</param>
        public void SetWindow(IntPtr hwnd, ref RECT rect)
        {
            this.parentHwnd = hwnd;
            this.windowBounds = rect.ToRectangle();
            this.previewControl.SetWindow(hwnd, this.windowBounds);
        }

        /// <summary>
        /// todo.
        /// </summary>
        /// <param name="rect">rectan.</param>
        public void SetRect(ref RECT rect)
        {
            this.windowBounds = rect.ToRectangle();
            this.previewControl.SetRect(this.windowBounds);
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void Unload()
        {
            this.previewControl.Unload();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void SetFocus()
        {
            this.previewControl.SetFocus();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="phwnd">Handle.</param>
        public void QueryFocus(out IntPtr phwnd)
        {
            this.previewControl.QueryFocus(out IntPtr result);
            phwnd = result;
            if (phwnd == IntPtr.Zero)
            {
                throw new Win32Exception();
            }
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="pmsg">Message.</param>
        /// <returns>Temp.</returns>
        public uint TranslateAccelerator(ref MSG pmsg)
        {
            if (this.frame != null)
            {
                return this.frame.TranslateAccelerator(ref pmsg);
            }

            const uint S_FALSE = 1;
            return S_FALSE;
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="phwnd">Handle.</param>
        public void GetWindow(out IntPtr phwnd)
        {
            phwnd = this.previewControl.GetHandle();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="fEnterMode">Temp.</param>
        public void ContextSensitiveHelp(bool fEnterMode)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// too.
        /// </summary>
        /// <param name="pUnkSite">Temp.</param>
        public void SetSite(object pUnkSite)
        {
            this.unkSite = pUnkSite;
            this.frame = this.unkSite as IPreviewHandlerFrame;
        }

        /// <summary>
        /// todo.
        /// </summary>
        /// <param name="riid">Rid.</param>
        /// <param name="ppvSite">Site.</param>
        public void GetSite(ref Guid riid, out object ppvSite)
        {
            ppvSite = this.unkSite;
        }

        /// <summary>
        /// todo.
        /// </summary>
        /// <param name="color">Colorr.</param>
        public void SetBackgroundColor(COLORREF color)
        {
            var argbColor = color.Color;
            this.previewControl.SetBackgroundColor(argbColor);
        }

        /// <inheritdoc />
        public void SetFont(ref LOGFONT plf)
        {
            var font = Font.FromLogFont(plf);
            this.previewControl.SetFont(font);
        }

        /// <inheritdoc />
        public void SetTextColor(COLORREF color)
        {
            var argbColor = color.Color;
            this.previewControl.SetTextColor(argbColor);
        }

        /// <summary>
        /// Tdod.
        /// </summary>
        /// <returns>Todo.</returns>
        protected abstract IPreviewHandlerControl CreatePreviewHandlerControl();
    }
}
