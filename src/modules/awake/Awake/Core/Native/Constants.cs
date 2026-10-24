// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Awake.Core.Native
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Win32 API convention.")]
    internal sealed class Constants
    {
        // Window Messages
        internal const uint WM_COMMAND = 0x0111;
        internal const uint WM_USER = 0x0400U;
        internal const uint WM_CLOSE = 0x0010;
        internal const int WM_CREATE = 0x0001;
        internal const int WM_DESTROY = 0x0002;
        internal const int WM_LBUTTONDOWN = 0x0201;
        internal const int WM_RBUTTONDOWN = 0x0204;
        internal const uint WM_POWERBROADCAST = 0x0218;
        internal const uint WM_ACTIVATE = 0x0006;
        internal const uint WM_SETFOCUS = 0x0007;
        internal const uint WM_ERASEBKGND = 0x0014;
        internal const uint WM_NEXTDLGCTL = 0x0028;
        internal const uint WM_NCDESTROY = 0x0082;
        internal const uint WM_GETDLGCODE = 0x0087;
        internal const uint WM_KEYDOWN = 0x0100;
        internal const uint WM_CHAR = 0x0102;
        internal const uint WM_INITDIALOG = 0x0110;
        internal const uint WM_CTLCOLORSTATIC = 0x0138;
        internal const uint WM_MOUSEWHEEL = 0x020A;

        // Power Broadcast Event Types
        internal const int PBT_APMRESUMEAUTOMATIC = 0x0012;
        internal const int PBT_APMRESUMESUSPEND = 0x0007;
        internal const int PBT_APMPOWERSTATUSCHANGE = 0x000A;

        // Menu Flags
        internal const uint MF_BYPOSITION = 1024;
        internal const uint MF_STRING = 0;
        internal const uint MF_SEPARATOR = 0x00000800;
        internal const uint MF_POPUP = 0x00000010;
        internal const uint MF_UNCHECKED = 0x00000000;
        internal const uint MF_CHECKED = 0x00000008;
        internal const uint MF_ENABLED = 0x00000000;
        internal const uint MF_DISABLED = 0x00000002;

        // Standard Handles
        internal const int STD_OUTPUT_HANDLE = -11;

        // Generic Access Rights
        internal const uint GENERIC_WRITE = 0x40000000;
        internal const uint GENERIC_READ = 0x80000000;

        // Notification Icons
        internal const int NIF_ICON = 0x00000002;
        internal const int NIF_MESSAGE = 0x00000001;
        internal const int NIF_TIP = 0x00000004;
        internal const int NIM_ADD = 0x00000000;
        internal const int NIM_DELETE = 0x00000002;
        internal const int NIM_MODIFY = 0x00000001;

        // Track Popup Menu Flags
        internal const uint TPM_LEFT_ALIGN = 0x0000;
        internal const uint TPM_BOTTOMALIGN = 0x0020;
        internal const uint TPM_LEFT_BUTTON = 0x0000;

        // Menu Item Info Flags
        internal const uint MNS_AUTO_DISMISS = 0x10000000;
        internal const uint MIM_STYLE = 0x00000010;

        // Attach Console
        internal const int ATTACH_PARENT_PROCESS = -1;

        // Dialogs
        internal const int IDOK = 1;
        internal const int IDCANCEL = 2;
        internal const uint MB_OK = 0x00000000;
        internal const uint MB_ICONWARNING = 0x00000030;
        internal const int WA_INACTIVE = 0;
        internal const nint DLGC_WANTTAB = 0x0002;
        internal const int DWLP_MSGRESULT = 0;

        // Window and Dialog Styles
        internal const uint WS_POPUP = 0x80000000;
        internal const uint WS_CHILD = 0x40000000;
        internal const uint WS_VISIBLE = 0x10000000;
        internal const uint WS_CAPTION = 0x00C00000;
        internal const uint WS_SYSMENU = 0x00080000;
        internal const uint WS_TABSTOP = 0x00010000;
        internal const uint WS_EX_APPWINDOW = 0x00040000;
        internal const uint DS_SETFONT = 0x0040;
        internal const uint DS_MODALFRAME = 0x0080;
        internal const uint DS_SETFOREGROUND = 0x0200;
        internal const uint DS_CENTER = 0x0800;
        internal const uint BS_DEFPUSHBUTTON = 0x0001;

        // Date and Time Picker
        internal const uint ICC_DATE_CLASSES = 0x00000100;
        internal const uint DTS_SHORTDATEFORMAT = 0x0000;
        internal const uint DTS_TIMEFORMAT = 0x0009;
        internal const uint DTM_GETSYSTEMTIME = 0x1001;
        internal const uint DTM_SETSYSTEMTIME = 0x1002;
        internal const uint DTM_SETRANGE = 0x1004;
        internal const uint DTM_GETMONTHCAL = 0x1008;
        internal const uint DTM_GETIDEALSIZE = 0x100F;
        internal const uint DTM_SETFORMAT = 0x1032;
        internal const uint GDT_VALID = 0;
        internal const uint GDTR_MIN = 0x0001;
        internal const uint GDTR_MAX = 0x0002;

        // Window Position and Hit Testing
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_NOZORDER = 0x0004;
        internal const uint SWP_NOACTIVATE = 0x0010;
        internal const uint GA_ROOT = 2;
        internal const uint CWP_SKIPINVISIBLE = 0x0001;

        // Locale
        internal const uint LOCALE_SSHORTDATE = 0x0000001F;
        internal const uint LOCALE_SSHORTTIME = 0x00000079;

        // Keyboard and Mouse
        internal const int WH_MOUSE_LL = 14;
        internal const int WHEEL_DELTA = 120;
        internal const nuint VK_TAB = 0x09;
        internal const int VK_SHIFT = 0x10;
        internal const int VK_CONTROL = 0x11;
        internal const nuint VK_LEFT = 0x25;
        internal const nuint VK_UP = 0x26;
        internal const nuint VK_RIGHT = 0x27;

        // System Colors
        internal const int COLOR_WINDOW = 5;
        internal const int COLOR_WINDOWTEXT = 8;
        internal const int COLOR_BTNFACE = 15;
    }
}
