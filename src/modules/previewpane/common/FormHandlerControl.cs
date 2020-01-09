using Common.ComInterlop;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace common
{
    /// <summary>
    /// Todo.
    /// </summary>
    public class FormHandlerControl : UserControl, IPreviewHandlerControl
    {
        private IntPtr _parentHwnd;

        private void InvokeOnControlThread(MethodInvoker d)
        {
            Invoke(d);
        }

        private void UpdateWindowBounds(Rectangle windowBounds)
        {
            if (Visible)
            {
                InvokeOnControlThread(delegate ()
                {
                    SetParent(Handle, _parentHwnd);
                    Bounds = windowBounds;
                    Visible = true;
                });
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        /// <summary>
        /// Todo.
        /// </summary>
        /// <returns></returns>
        public IntPtr GetHandle()
        {
            return Handle;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetFocus();

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="result"></param>
        public void QueryFocus(out IntPtr result)
        {
            var getResult = IntPtr.Zero;
            InvokeOnControlThread(delegate () { getResult = GetFocus(); });
            result = getResult;
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="argbColor"></param>
        public void SetBackgroundColor(Color argbColor)
        {
            InvokeOnControlThread(delegate () { BackColor = argbColor; });
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void SetFocus()
        {
            InvokeOnControlThread(delegate () { Focus(); });
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="font"></param>
        public void SetFont(Font font)
        {
            InvokeOnControlThread(delegate () { Font = font; });
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="WindowBounds"></param>
        public void SetRect(Rectangle WindowBounds)
        {
            UpdateWindowBounds(WindowBounds);
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="color"></param>
        public void SetTextColor(Color color)
        {
            InvokeOnControlThread(delegate () { ForeColor = color; });
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="rect"></param>
        public void SetWindow(IntPtr hwnd, Rectangle rect)
        {
            _parentHwnd = hwnd;
            UpdateWindowBounds(rect);
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void Unload()
        {
            InvokeOnControlThread(delegate ()
            {
                Visible = false;
                foreach (Control c in Controls) c.Dispose();
                Controls.Clear();
            });
        }
    }
}
