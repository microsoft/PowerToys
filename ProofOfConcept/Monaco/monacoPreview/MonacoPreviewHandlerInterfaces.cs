using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace monacoPreview
{

    public struct RECT
    {
        public int Left { get; set; }

        public int Top { get; set; }

        public int Right { get; set; }

        public int Bottom { get; set; }

        public Rectangle ToRectangle()
        {
            return Rectangle.FromLTRB(Left, Top, Right, Bottom);
        }
    }

    public struct MSG
    {
        public IntPtr Hwnd { get; set; }

        public int Message { get; set; }

        public IntPtr WParam { get; set; }
        public IntPtr LParam { get; set; }

        public int Time { get; set; }

        public int PtX { get; set; }

        public int PtY { get; set; }
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("cfac301a-fdd7-4e54-aa10-318ffa7f7bd2")]
    public interface IPreviewHandler
    {
        void SetWindow(IntPtr hwnd, ref RECT rect);

        void SetRect(ref RECT rect);

        void DoPreview();

        void Unload();

        void SetFocus();

        void QueryFocus(out IntPtr phwnd);

        [PreserveSig]
        uint TranslateAccelerator(ref MSG pmsg);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("cfac301a-fdd7-4e54-aa10-318ffa7f7bd2")]
    public interface IInitializeWithStream
    {
        void Initialize(IStream pstream, uint grfMode);
    }

    [ComImport]
    [Guid("cfac301a-fdd7-4e54-aa10-318ffa7f7bd2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IOleWindow
    {
        void GetWindow(out IntPtr phwnd);

        void ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool fEnterMode);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("fc4801a3-2ba9-11cf-a229-00aa003d7352")]
    public interface IObjectWithSite
    {
        void SetSite([In, MarshalAs(UnmanagedType.IUnknown)] object pUnkSite);

        void GetSite(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppvSite);
    }
}
