using Common.ComInterlop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace common
{
    /// <summary>
    /// Todo.
    /// </summary>
    public abstract class PreviewHandler : IPreviewHandler, IOleWindow, IObjectWithSite, IPreviewHandlerVisuals
    {
        private bool _showPreview;
        private IPreviewHandlerControl _previewControl;
        private IntPtr _parentHwnd;
        private Rectangle _windowBounds;
        private object _unkSite;
        private IPreviewHandlerFrame _frame;

        /// <summary>
        /// Todo.
        /// </summary>
        public abstract void DoPreview();

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="hwnd">Todo.</param>
        /// <param name="rect">Todo.</param>
        public void SetWindow(IntPtr hwnd, ref RECT rect) 
        {
            _parentHwnd = hwnd;
            _windowBounds = rect.ToRectangle();
            _previewControl.SetWindow(hwnd, _windowBounds);
        }

        /// <summary>
        /// todo.
        /// </summary>
        /// <param name="rect"></param>
        public void SetRect(ref RECT rect)
        {
            _windowBounds = rect.ToRectangle();
            _previewControl.SetRect(_windowBounds);
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void Unload() 
        {
            _showPreview = false;
            _previewControl.Unload();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void SetFocus() 
        {
            _previewControl.SetFocus();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="phwnd"></param>
        public void QueryFocus(out IntPtr phwnd) 
        {
            IntPtr result = IntPtr.Zero;
            _previewControl.QueryFocus(out result);
            phwnd = result;
            if (phwnd == IntPtr.Zero) throw new Win32Exception();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="pmsg"></param>
        /// <returns></returns>
        public uint TranslateAccelerator(ref MSG pmsg)
        {
            if (_frame != null) return _frame.TranslateAccelerator(ref pmsg);
            const uint S_FALSE = 1;
            return S_FALSE;
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="phwnd"></param>
        public void GetWindow(out IntPtr phwnd)
        {
            phwnd = _previewControl.GetHandle();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="fEnterMode"></param>
        public void ContextSensitiveHelp(bool fEnterMode)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// too.
        /// </summary>
        /// <param name="pUnkSite"></param>
        public void SetSite(object pUnkSite)
        {
            _unkSite = pUnkSite;
            _frame = _unkSite as IPreviewHandlerFrame;
        }

        /// <summary>
        /// todo.
        /// </summary>
        /// <param name="riid"></param>
        /// <param name="ppvSite"></param>
        public void GetSite(ref Guid riid, out object ppvSite)
        {
            ppvSite = _unkSite;
        }

        /// <summary>
        /// todo.
        /// </summary>
        /// <param name="color"></param>
        public void SetBackgroundColor(COLORREF color)
        {
            var argbColor = color.Color;
            _previewControl.SetBackgroundColor(argbColor);
        }

        /// <summary>
        /// todo.
        /// </summary>
        /// <param name="plf"></param>
        public void SetFont(ref LOGFONT plf)
        {
            var font = Font.FromLogFont(plf);
            _previewControl.SetFont(font);
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="color"></param>
        public void SetTextColor(COLORREF color)
        {
            var argbColor = color.Color;
            _previewControl.SetTextColor(argbColor);
        }
    }
}