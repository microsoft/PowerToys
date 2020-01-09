using Common.ComInterlop;
using System;
using System.Drawing;

namespace common
{
    /// <summary>
    /// Todo.
    /// </summary>
    public interface IPreviewHandlerControl
    {
        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="result"></param>
        void QueryFocus(out IntPtr result);
        
        /// <summary>
        /// Todo.
        /// </summary>
        void SetFocus();

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="font"></param>
        void SetFont(Font font);

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="color"></param>
        void SetTextColor(Color color);

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="argbColor"></param>
        void SetBackgroundColor(Color argbColor);

        /// <summary>
        /// Todo.
        /// </summary>
        /// <returns></returns>
        IntPtr GetHandle();

        /// <summary>
        /// Todo.
        /// </summary>
        void Unload();

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="_windowBounds"></param>
        void SetRect(Rectangle _windowBounds);

        /// <summary>
        /// Todo.
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="rect"></param>
        void SetWindow(IntPtr hwnd, Rectangle rect);
    }
}