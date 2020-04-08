// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Common.ComInterlop;
using Microsoft.Win32;

namespace Common
{
    /// <summary>
    /// Preview Handler base class implmenenting interfaces required by Preview Handler.
    /// </summary>
    public abstract class PreviewHandlerBase : IPreviewHandler, IOleWindow, IObjectWithSite, IPreviewHandlerVisuals
    {
        /// <summary>
        /// An instance of Preview Control Used by the Handler.
        /// </summary>
        private IPreviewHandlerControl previewControl;

        /// <summary>
        /// Hold reference for the window handle.
        /// </summary>
        private IntPtr parentHwnd;

        /// <summary>
        /// Hold the bounds of the window.
        /// </summary>
        private Rectangle windowBounds;

        /// <summary>
        /// Holds the site pointer.
        /// </summary>
        private object unkSite;

        /// <summary>
        /// Holds reference for the IPreviewHandlerFrame.
        /// </summary>
        private IPreviewHandlerFrame frame;

        /// <summary>
        /// Initializes a new instance of the <see cref="PreviewHandlerBase"/> class.
        /// </summary>
        public PreviewHandlerBase()
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
            this.previewControl.SetBackgroundColor(color.Color);
        }

        /// <inheritdoc />
        public void SetFont(ref LOGFONT plf)
        {
            this.previewControl.SetFont(Font.FromLogFont(plf));
        }

        /// <inheritdoc />
        public void SetTextColor(COLORREF color)
        {
            this.previewControl.SetTextColor(color.Color);
        }

        /// <summary>
        /// Provide instance of the implementation of <see cref="IPreviewHandlerControl"/>. Should be overide by the implementation class with control object to be used.
        /// </summary>
        /// <returns>Instance of the <see cref="IPreviewHandlerControl"/>.</returns>
        protected abstract IPreviewHandlerControl CreatePreviewHandlerControl();
    }
}
