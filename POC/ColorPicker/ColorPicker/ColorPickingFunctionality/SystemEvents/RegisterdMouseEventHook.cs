using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ColorPicker.ColorPickingFunctionality.SystemEvents
{
    class RegisterdMouseEventHook : SystemHook
    {
        public delegate void EventCallBack(int x, int y);

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private EventCallBack callback;

        public RegisterdMouseEventHook(EventCallBack callback) : base(WH_MOUSE_LL)
        {
            this.callback = callback;
        }

        public override int HookProc(int nCode, int wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == WM_LBUTTONDOWN)
            {
                MSLLHOOKSTRUCT mouseHookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                callback(mouseHookStruct.pt.x, mouseHookStruct.pt.y);
            }
            return CallNextHookExWrapper(nCode, wParam, lParam);
        }
    }
}

