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
    /// Preview Handler base class implmenenting interfaces required by Preview Handler.
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

        /// <inheritdoc />
        public abstract void DoPreview();

        /// <inheritdoc />
        public void SetWindow(IntPtr hwnd, ref RECT rect)
        {
            this.parentHwnd = hwnd;
            this.windowBounds = rect.ToRectangle();
            this.previewControl.SetWindow(hwnd, this.windowBounds);
        }

        /// <inheritdoc />
        public void SetRect(ref RECT rect)
        {
            this.windowBounds = rect.ToRectangle();
            this.previewControl.SetRect(this.windowBounds);
        }

        /// <inheritdoc />
        public void Unload()
        {
            this.previewControl.Unload();
        }

        /// <inheritdoc />
        public void SetFocus()
        {
            this.previewControl.SetFocus();
        }

        /// <inheritdoc />
        public void QueryFocus(out IntPtr phwnd)
        {
            this.previewControl.QueryFocus(out IntPtr result);
            phwnd = result;
            if (phwnd == IntPtr.Zero)
            {
                throw new Win32Exception();
            }
        }

        /// <inheritdoc />
        public uint TranslateAccelerator(ref MSG pmsg)
        {
            // Current implementation simply directs all Keystrokes to IPreviewHandlerFrame. This is the recommended approach to handle keystokes for all low-integrity preview handlers.
            // Source: https://docs.microsoft.com/en-us/windows/win32/shell/building-preview-handlers#ipreviewhandlertranslateaccelerator
            if (this.frame != null)
            {
                return this.frame.TranslateAccelerator(ref pmsg);
            }

            const uint S_FALSE = 1;
            return S_FALSE;
        }

        /// <inheritdoc />
        public void GetWindow(out IntPtr phwnd)
        {
            phwnd = this.previewControl.GetHandle();
        }

        /// <inheritdoc />
        public void ContextSensitiveHelp(bool fEnterMode)
        {
            // Should always return NotImplementedException. Source: https://docs.microsoft.com/en-us/windows/win32/shell/building-preview-handlers#iolewindowcontextsensitivehelp
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void SetSite(object pUnkSite)
        {
            // Implementation logic details: https://docs.microsoft.com/en-us/windows/win32/shell/building-preview-handlers#iobjectwithsitesetsite
            this.unkSite = pUnkSite;
            this.frame = this.unkSite as IPreviewHandlerFrame;
        }

        /// <inheritdoc />
        public void GetSite(ref Guid riid, out object ppvSite)
        {
            ppvSite = this.unkSite;
        }

        /// <inheritdoc />
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
        /// Provide instance of the implementation of <see cref="IPreviewHandlerControl"/>. Should be overide by the implementation class with control object to be used.
        /// </summary>
        /// <returns>Instance of the <see cref="IPreviewHandlerControl"/>.</returns>
        protected abstract IPreviewHandlerControl CreatePreviewHandlerControl();
    }
}
