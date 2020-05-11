using System;
using System.Runtime.InteropServices;

namespace ColorPicker.ColorPickingFunctionality.SystemEvents
{
    class MouseEvent : SystemHook
    {
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

        public delegate void EventCallBack(int x, int y);
        private EventCallBack callBack;

        public MouseEvent(EventCallBack callBack) : base(WH_MOUSE_LL)
        {
            this.callBack = callBack;
        }

        public override int HookProc(int nCode, int wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == WM_LBUTTONDOWN)
            {
                MSLLHOOKSTRUCT mouseHookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                callBack(mouseHookStruct.pt.x, mouseHookStruct.pt.y);
            }
            //propogates the click event through to other listeners
            return CallNextHookExWrapper(nCode, wParam, lParam);
        }
    }
}

