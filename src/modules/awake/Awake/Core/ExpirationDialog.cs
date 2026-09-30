// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Awake.Core.Models;
using Awake.Core.Native;
using Awake.Properties;
using ManagedCommon;

namespace Awake.Core
{
    // Awake has no UI framework, so the dialog is built from an in-memory template;
    // the native pickers bring keyboard navigation, DPI scaling and regional formats.
    internal static class ExpirationDialog
    {
        private const int DatePickerId = 101;
        private const int TimePickerId = 102;
        private const string DateTimePickerClass = "SysDateTimePick32";

        private enum PickerField
        {
            Day,
            Month,
            Year,
            Hour,
            Minute,
            AmPm,
        }

        private const short ButtonStripTop = 46;

        // Keeps date arithmetic and the conversion to DateTimeOffset clear of DateTime.MaxValue.
        private static readonly DateTime LatestExpiration = new(9998, 12, 31, 23, 59, 0);

        // Kept in fields so the delegates are not collected while the dialog is open.
        private static readonly Bridge.DialogProcDelegate DialogProcInstance = DialogProc;
        private static readonly Bridge.SubclassProcDelegate PickerSubclassProcInstance = PickerSubclassProc;
        private static readonly Bridge.LowLevelMouseProcDelegate MouseHookProcInstance = MouseHookProc;

        private static IntPtr _dialogHandle;
        private static IntPtr _mouseHook;
        private static IntPtr _wheelTarget;
        private static int _wheelDelta;
        private static PickerField[] _dateFields = [];
        private static PickerField[] _timeFields = [];
        private static DateTimeOffset _suggestion;
        private static DateTimeOffset _expireAt;

        internal static bool TryPick(IntPtr owner, DateTimeOffset suggestion, out DateTimeOffset expireAt)
        {
            expireAt = default;

            // The tray menu stays usable while the dialog is open, so a second click would stack another one.
            if (_dialogHandle != IntPtr.Zero)
            {
                Bridge.SetForegroundWindow(_dialogHandle);
                return false;
            }

            InitCommonControlsEx controls = new()
            {
                DwSize = (uint)Marshal.SizeOf<InitCommonControlsEx>(),
                DwIcc = Native.Constants.ICC_DATE_CLASSES,
            };
            Bridge.InitCommonControlsEx(ref controls);

            _suggestion = suggestion;
            _wheelTarget = IntPtr.Zero;
            _wheelDelta = 0;

            byte[] template = BuildTemplate();
            GCHandle pinnedTemplate = GCHandle.Alloc(template, GCHandleType.Pinned);
            try
            {
                IntPtr result = Bridge.DialogBoxIndirectParam(
                    Marshal.GetHINSTANCE(typeof(Program).Module),
                    pinnedTemplate.AddrOfPinnedObject(),
                    owner,
                    DialogProcInstance,
                    IntPtr.Zero);

                if (result == -1)
                {
                    Logger.LogError($"Failed to show the expiration dialog. Error code: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                if (result == Native.Constants.IDOK)
                {
                    expireAt = _expireAt;
                    return true;
                }

                return false;
            }
            finally
            {
                SetMouseHook(false);
                pinnedTemplate.Free();
                _dialogHandle = IntPtr.Zero;
            }
        }

        private static IntPtr DialogProc(IntPtr hDlg, uint message, IntPtr wParam, IntPtr lParam)
        {
            switch (message)
            {
                case Native.Constants.WM_INITDIALOG:
                    _dialogHandle = hDlg;
                    InitializePickers(hDlg);

                    // Starts on the hour: the time is what people adjust, the date is mostly right already.
                    IntPtr timePicker = Bridge.GetDlgItem(hDlg, TimePickerId);
                    Bridge.SendMessage(hDlg, Native.Constants.WM_NEXTDLGCTL, (nuint)timePicker, 1);
                    SelectField(timePicker, _timeFields, PickerField.Hour);
                    return 0;

                case Native.Constants.WM_MOUSEWHEEL:
                    // With "scroll inactive windows" turned off, Windows sends the wheel to the focused
                    // control, so it reaches the dialog through a focused button.
                    OnMouseWheel(wParam, lParam);
                    return 1;

                // The hook only matters while the dialog is in front, and every mouse event in the system
                // waits for this thread while it is installed.
                case Native.Constants.WM_ACTIVATE:
                    SetMouseHook((wParam.ToInt64() & 0xFFFF) != Native.Constants.WA_INACTIVE);
                    break;

                case Native.Constants.WM_ERASEBKGND:
                    PaintBackground(hDlg, wParam);
                    Bridge.SetWindowLongPtr(hDlg, Native.Constants.DWLP_MSGRESULT, 1);
                    return 1;

                case Native.Constants.WM_CTLCOLORSTATIC:
                    _ = Bridge.SetBkColor(wParam, Bridge.GetSysColor(Native.Constants.COLOR_WINDOW));
                    _ = Bridge.SetTextColor(wParam, Bridge.GetSysColor(Native.Constants.COLOR_WINDOWTEXT));
                    return Bridge.GetSysColorBrush(Native.Constants.COLOR_WINDOW);

                case Native.Constants.WM_COMMAND:
                    switch ((int)(wParam.ToInt64() & 0xFFFF))
                    {
                        case Native.Constants.IDOK:
                            DateTimeOffset expireAt = new(ReadPickers(hDlg));
                            if (expireAt <= DateTimeOffset.Now)
                            {
                                _ = Bridge.MessageBox(hDlg, Resources.AWAKE_EXPIRATION_DIALOG_PAST, Constants.FullAppName, Native.Constants.MB_OK | Native.Constants.MB_ICONWARNING);
                                return 1;
                            }

                            _expireAt = expireAt;
                            Bridge.EndDialog(hDlg, Native.Constants.IDOK);
                            return 1;

                        case Native.Constants.IDCANCEL:
                            Bridge.EndDialog(hDlg, Native.Constants.IDCANCEL);
                            return 1;
                    }

                    break;
            }

            return 0;
        }

        private static void InitializePickers(IntPtr hDlg)
        {
            IntPtr datePicker = Bridge.GetDlgItem(hDlg, DatePickerId);
            IntPtr timePicker = Bridge.GetDlgItem(hDlg, TimePickerId);

            SystemTime[] range = [SystemTime.FromDateTime(DateTime.Today), SystemTime.FromDateTime(LatestExpiration)];
            Bridge.SendMessage(datePicker, Native.Constants.DTM_SETRANGE, Native.Constants.GDTR_MIN | Native.Constants.GDTR_MAX, range);

            // The calendar button keeps its own gap to the text, the up-down control does not.
            _dateFields = FieldOrder(SetPaddedFormat(datePicker, Native.Constants.LOCALE_SSHORTDATE, padEnd: false));

            // The picker's default time format includes seconds.
            _timeFields = FieldOrder(SetPaddedFormat(timePicker, Native.Constants.LOCALE_SSHORTTIME, padEnd: true));

            // Widest value (two-digit fields, PM), next year's to stay within the range.
            WritePickers(hDlg, new DateTime(DateTime.Today.Year + 1, 12, 28, 22, 58, 0));
            FitPickers(hDlg, datePicker, timePicker);

            WritePickers(hDlg, _suggestion.LocalDateTime);

            Bridge.SetWindowSubclass(datePicker, PickerSubclassProcInstance, DatePickerId, 0);
            Bridge.SetWindowSubclass(timePicker, PickerSubclassProcInstance, TimePickerId, 0);
        }

        // Regional date and time formats differ in width.
        private static void FitPickers(IntPtr hDlg, IntPtr datePicker, IntPtr timePicker)
        {
            Size dateSize = default;
            Size timeSize = default;
            Bridge.SendMessage(datePicker, Native.Constants.DTM_GETIDEALSIZE, 0, ref dateSize);
            Bridge.SendMessage(timePicker, Native.Constants.DTM_GETIDEALSIZE, 0, ref timeSize);

            const uint moveOnly = Native.Constants.SWP_NOZORDER | Native.Constants.SWP_NOACTIVATE;
            Rect dateBounds = BoundsInDialog(hDlg, datePicker);
            Rect gap = new() { Right = 4 };
            Bridge.MapDialogRect(hDlg, ref gap);

            int height = dateBounds.Bottom - dateBounds.Top;
            int timeLeft = dateBounds.Left + dateSize.Width + gap.Right;
            Bridge.SetWindowPos(datePicker, IntPtr.Zero, dateBounds.Left, dateBounds.Top, dateSize.Width, height, moveOnly);
            Bridge.SetWindowPos(timePicker, IntPtr.Zero, timeLeft, dateBounds.Top, timeSize.Width, height, moveOnly);

            Bridge.GetClientRect(hDlg, out Rect client);
            int overflow = timeLeft + timeSize.Width + dateBounds.Left - client.Right;
            if (overflow <= 0)
            {
                return;
            }

            Bridge.GetWindowRect(hDlg, out Rect window);
            Bridge.SetWindowPos(hDlg, IntPtr.Zero, window.Left - (overflow / 2), window.Top, window.Right - window.Left + overflow, window.Bottom - window.Top, moveOnly);

            foreach (int buttonId in new[] { Native.Constants.IDOK, Native.Constants.IDCANCEL })
            {
                IntPtr button = Bridge.GetDlgItem(hDlg, buttonId);
                Rect buttonBounds = BoundsInDialog(hDlg, button);
                Bridge.SetWindowPos(button, IntPtr.Zero, buttonBounds.Left + overflow, buttonBounds.Top, 0, 0, moveOnly | Native.Constants.SWP_NOSIZE);
            }
        }

        private static Rect BoundsInDialog(IntPtr hDlg, IntPtr control)
        {
            Bridge.GetWindowRect(control, out Rect rect);
            _ = Bridge.MapWindowPoints(IntPtr.Zero, hDlg, ref rect, 2);
            return rect;
        }

        // The picker has no padding setting and draws its text against the border, so the
        // format gets en spaces; a regular space is too narrow to be noticed.
        private static string SetPaddedFormat(IntPtr picker, uint localeFormat, bool padEnd)
        {
            char[] buffer = new char[80];
            int length = Bridge.GetLocaleInfoEx(null, localeFormat, buffer, buffer.Length);
            if (length <= 1)
            {
                return string.Empty;
            }

            string format = new(buffer, 0, length - 1);
            Bridge.SendMessage(picker, Native.Constants.DTM_SETFORMAT, 0, "\u2002" + format + (padEnd ? "\u2002" : string.Empty));
            return format;
        }

        // Tab needs the first and the last field.
        private static PickerField[] FieldOrder(string format)
        {
            List<PickerField> fields = [];
            bool quoted = false;
            foreach (char c in format)
            {
                if (c == '\'')
                {
                    quoted = !quoted;
                    continue;
                }

                PickerField? field = quoted ? null : c switch
                {
                    'd' => PickerField.Day,
                    'M' => PickerField.Month,
                    'y' => PickerField.Year,
                    'h' or 'H' => PickerField.Hour,
                    'm' => PickerField.Minute,
                    't' => PickerField.AmPm,
                    _ => null,
                };
                if (field is PickerField known && !fields.Contains(known))
                {
                    fields.Add(known);
                }
            }

            return [.. fields];
        }

        // The native pickers ignore the mouse wheel and leave Tab to the dialog.
        private static IntPtr PickerSubclassProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData)
        {
            switch (message)
            {
                case Native.Constants.WM_MOUSEWHEEL:
                    OnMouseWheel(wParam, lParam);
                    return 0;

                // Tab steps through the fields and leaves the picker from the last one.
                // While the calendar is open, the arrow keys of the probe would move its selection instead.
                case Native.Constants.WM_GETDLGCODE:
                    if (lParam != IntPtr.Zero &&
                        Marshal.PtrToStructure<Msg>(lParam) is { Message: Native.Constants.WM_KEYDOWN } key &&
                        key.WParam == (nint)Native.Constants.VK_TAB &&
                        Bridge.SendMessage(hWnd, Native.Constants.DTM_GETMONTHCAL, 0, 0) == IntPtr.Zero &&
                        HasFieldInTabDirection(hWnd, uIdSubclass))
                    {
                        return Bridge.DefSubclassProc(hWnd, message, wParam, lParam) | Native.Constants.DLGC_WANTTAB;
                    }

                    break;

                case Native.Constants.WM_SETFOCUS:
                    IntPtr result = Bridge.DefSubclassProc(hWnd, message, wParam, lParam);
                    PickerField[] fields = FieldsOf(uIdSubclass);
                    bool fromSibling = Bridge.GetParent(wParam) == _dialogHandle;
                    if (fromSibling && Bridge.GetKeyState((int)Native.Constants.VK_TAB) < 0 && fields.Length > 0)
                    {
                        SelectField(hWnd, fields, IsShiftDown() ? fields[^1] : fields[0]);
                    }

                    return result;

                case Native.Constants.WM_KEYDOWN when wParam == (nint)Native.Constants.VK_TAB:
                    Bridge.SendMessage(hWnd, Native.Constants.WM_KEYDOWN, IsShiftDown() ? Native.Constants.VK_LEFT : Native.Constants.VK_RIGHT, 0);
                    return 0;

                case Native.Constants.WM_CHAR when wParam == (nint)Native.Constants.VK_TAB:
                    return 0;

                case Native.Constants.WM_NCDESTROY:
                    Bridge.RemoveWindowSubclass(hWnd, PickerSubclassProcInstance, uIdSubclass);
                    break;
            }

            return Bridge.DefSubclassProc(hWnd, message, wParam, lParam);
        }

        private static void OnMouseWheel(IntPtr wParam, IntPtr lParam)
        {
            Point pointer = new() { X = (short)(lParam.ToInt64() & 0xFFFF), Y = (short)((lParam.ToInt64() >> 16) & 0xFFFF) };
            WheelPicker(PickerAt(pointer), (short)((wParam.ToInt64() >> 16) & 0xFFFF));
        }

        private static void SetMouseHook(bool installed)
        {
            if (installed && _mouseHook == IntPtr.Zero)
            {
                _mouseHook = Bridge.SetWindowsHookEx(Native.Constants.WH_MOUSE_LL, MouseHookProcInstance, Bridge.GetModuleHandle(null), 0);
                if (_mouseHook == IntPtr.Zero)
                {
                    Logger.LogError($"Failed to install the mouse hook of the expiration dialog. Error code: {Marshal.GetLastWin32Error()}");
                }
            }
            else if (!installed && _mouseHook != IntPtr.Zero)
            {
                Bridge.UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
        }

        // While the dialog is in front, the wheel steps the focused field also over the
        // dialog's background and the desktop, where it would otherwise do nothing.
        private static IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 &&
                wParam == (nint)Native.Constants.WM_MOUSEWHEEL &&
                _dialogHandle != IntPtr.Zero &&
                Bridge.GetForegroundWindow() == _dialogHandle &&
                Bridge.GetAsyncKeyState(Native.Constants.VK_CONTROL) >= 0 &&
                Bridge.SendMessage(Bridge.GetDlgItem(_dialogHandle, DatePickerId), Native.Constants.DTM_GETMONTHCAL, 0, 0) == IntPtr.Zero &&
                Marshal.PtrToStructure<MsllHookStruct>(lParam) is var wheel &&
                IsDialogOrDesktop(wheel.Position))
            {
                IntPtr picker = PickerAt(wheel.Position);
                if (picker == IntPtr.Zero)
                {
                    picker = FocusedPicker();
                }

                if (picker != IntPtr.Zero)
                {
                    WheelPicker(picker, (short)(wheel.MouseData >> 16));
                    return 1;
                }
            }

            return Bridge.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private static bool IsDialogOrDesktop(Point screenPoint)
        {
            IntPtr window = Bridge.GetAncestor(Bridge.WindowFromPoint(screenPoint), Native.Constants.GA_ROOT);
            if (window == _dialogHandle)
            {
                return true;
            }

            // Progman shows the desktop icons; WorkerW hosts them once Explorer has split the desktop for a
            // wallpaper transition (before Windows 11 24H2).
            char[] buffer = new char[16];
            string windowClass = new(buffer, 0, Bridge.GetClassName(window, buffer, buffer.Length));
            return windowClass is "Progman" or "WorkerW";
        }

        private static IntPtr FocusedPicker() => AsPicker(Bridge.GetFocus());

        private static IntPtr AsPicker(IntPtr window) =>
            window == Bridge.GetDlgItem(_dialogHandle, DatePickerId) || window == Bridge.GetDlgItem(_dialogHandle, TimePickerId)
                ? window
                : IntPtr.Zero;

        private static void WheelPicker(IntPtr picker, int delta)
        {
            if (picker == IntPtr.Zero)
            {
                return;
            }

            if (picker != _wheelTarget)
            {
                _wheelTarget = picker;
                _wheelDelta = 0;
            }

            if (Bridge.GetFocus() != picker)
            {
                Bridge.SendMessage(_dialogHandle, Native.Constants.WM_NEXTDLGCTL, (nuint)picker, 1);
            }

            // Touchpads report fractions of a notch, so partial deltas add up until a full step.
            _wheelDelta += delta;
            int steps = _wheelDelta / Native.Constants.WHEEL_DELTA;
            _wheelDelta %= Native.Constants.WHEEL_DELTA;

            if (steps == 0)
            {
                return;
            }

            if (picker == Bridge.GetDlgItem(_dialogHandle, DatePickerId))
            {
                // Whole days rather than the selected field: the picker wraps a field within its
                // month, so on the 30th the day field could never reach tomorrow.
                MoveExpiration(ReadPickers(_dialogHandle), TimeSpan.FromDays(steps));
                return;
            }

            StepSelectedTimeField(picker, steps);
        }

        private static IntPtr PickerAt(Point screenPoint)
        {
            Bridge.ScreenToClient(_dialogHandle, ref screenPoint);
            return AsPicker(Bridge.ChildWindowFromPointEx(_dialogHandle, screenPoint, Native.Constants.CWP_SKIPINVISIBLE));
        }

        private static PickerField[] FieldsOf(nuint pickerId) => pickerId == DatePickerId ? _dateFields : _timeFields;

        private static bool HasFieldInTabDirection(IntPtr picker, nuint pickerId)
        {
            PickerField[] fields = FieldsOf(pickerId);
            int index = SelectedField(picker) is PickerField field ? Array.IndexOf(fields, field) : -1;
            return index >= 0 && (IsShiftDown() ? index > 0 : index < fields.Length - 1);
        }

        private static bool IsShiftDown() => Bridge.GetKeyState(Native.Constants.VK_SHIFT) < 0;

        // The picker wraps a field on its own, so 23:00 would step to 00:00 of the same day.
        // The step of the selected field is applied to date and time together instead.
        private static void StepSelectedTimeField(IntPtr timePicker, int steps)
        {
            TimeSpan step = SelectedField(timePicker) switch
            {
                PickerField.Hour => TimeSpan.FromHours(1),
                PickerField.Minute => TimeSpan.FromMinutes(1),
                PickerField.AmPm => TimeSpan.FromHours(12),
                _ => TimeSpan.Zero,
            };

            for (int i = 0; i < Math.Abs(steps) && step != TimeSpan.Zero; i++)
            {
                MoveExpiration(ReadPickers(_dialogHandle), steps > 0 ? step : -step);
            }
        }

        // The picker does not report its selected field, so it is found by stepping a probe value with an
        // arrow key and writing the real value back. The probe sits mid-month, mid-year and mid-morning,
        // where no field wraps or hits the date range.
        private static PickerField? SelectedField(IntPtr picker)
        {
            SystemTime value = default;
            Bridge.SendMessage(picker, Native.Constants.DTM_GETSYSTEMTIME, 0, ref value);
            SystemTime probe = SystemTime.FromDateTime(new DateTime(DateTime.Today.Year + 1, 6, 15, 10, 30, 0));
            SystemTime stepped = probe;
            Bridge.SendMessage(picker, Native.Constants.DTM_SETSYSTEMTIME, Native.Constants.GDT_VALID, ref probe);
            Bridge.SendMessage(picker, Native.Constants.WM_KEYDOWN, Native.Constants.VK_UP, 0);
            Bridge.SendMessage(picker, Native.Constants.DTM_GETSYSTEMTIME, 0, ref stepped);
            Bridge.SendMessage(picker, Native.Constants.DTM_SETSYSTEMTIME, Native.Constants.GDT_VALID, ref value);

            return stepped.Year != probe.Year ? PickerField.Year
                : stepped.Month != probe.Month ? PickerField.Month
                : stepped.Day != probe.Day ? PickerField.Day
                : stepped.Hour - probe.Hour == 12 ? PickerField.AmPm
                : stepped.Hour != probe.Hour ? PickerField.Hour
                : stepped.Minute != probe.Minute ? PickerField.Minute
                : null;
        }

        // Counts the steps from one probe instead of probing after every arrow key.
        private static void SelectField(IntPtr picker, PickerField[] fields, PickerField target)
        {
            int from = SelectedField(picker) is PickerField field ? Array.IndexOf(fields, field) : -1;
            int to = Array.IndexOf(fields, target);
            if (from < 0 || to < 0)
            {
                return;
            }

            for (int i = 0; i < Math.Abs(to - from); i++)
            {
                Bridge.SendMessage(picker, Native.Constants.WM_KEYDOWN, to > from ? Native.Constants.VK_RIGHT : Native.Constants.VK_LEFT, 0);
            }
        }

        // Forward steps always apply, so a value already in the past can still be wheeled forward.
        private static void MoveExpiration(DateTime from, TimeSpan by)
        {
            DateTime to = from + by;
            bool allowed = to <= LatestExpiration && (by > TimeSpan.Zero || to > DateTime.Now);
            WritePickers(_dialogHandle, allowed ? to : from);
        }

        // White content area above a button strip, like the Windows message boxes.
        private static void PaintBackground(IntPtr hDlg, IntPtr hdc)
        {
            Bridge.GetClientRect(hDlg, out Rect client);

            Rect divider = new() { Top = ButtonStripTop };
            Bridge.MapDialogRect(hDlg, ref divider);

            Rect content = client with { Bottom = divider.Top };
            Rect buttonStrip = client with { Top = divider.Top };
            _ = Bridge.FillRect(hdc, ref content, Bridge.GetSysColorBrush(Native.Constants.COLOR_WINDOW));
            _ = Bridge.FillRect(hdc, ref buttonStrip, Bridge.GetSysColorBrush(Native.Constants.COLOR_BTNFACE));
        }

        private static DateTime ReadPickers(IntPtr hDlg)
        {
            SystemTime date = default;
            SystemTime time = default;
            Bridge.SendMessage(Bridge.GetDlgItem(hDlg, DatePickerId), Native.Constants.DTM_GETSYSTEMTIME, 0, ref date);
            Bridge.SendMessage(Bridge.GetDlgItem(hDlg, TimePickerId), Native.Constants.DTM_GETSYSTEMTIME, 0, ref time);

            return new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0, DateTimeKind.Local);
        }

        private static void WritePickers(IntPtr hDlg, DateTime value)
        {
            SystemTime systemTime = SystemTime.FromDateTime(value);
            Bridge.SendMessage(Bridge.GetDlgItem(hDlg, DatePickerId), Native.Constants.DTM_SETSYSTEMTIME, Native.Constants.GDT_VALID, ref systemTime);
            Bridge.SendMessage(Bridge.GetDlgItem(hDlg, TimePickerId), Native.Constants.DTM_SETSYSTEMTIME, Native.Constants.GDT_VALID, ref systemTime);
        }

        private static byte[] BuildTemplate()
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Unicode);

            writer.Write(Native.Constants.WS_POPUP | Native.Constants.WS_CAPTION | Native.Constants.WS_SYSMENU |
                         Native.Constants.DS_MODALFRAME | Native.Constants.DS_SETFONT | Native.Constants.DS_CENTER | Native.Constants.DS_SETFOREGROUND);

            // Gives the dialog a taskbar button, because its owner is the hidden tray window.
            writer.Write(Native.Constants.WS_EX_APPWINDOW);
            writer.Write((short)5);
            WriteRect(writer, 0, 0, 124, ButtonStripTop + 28);
            writer.Write((short)0); // no menu
            writer.Write((short)0); // default dialog class
            WriteString(writer, Constants.FullAppName);
            writer.Write((short)9);
            WriteString(writer, "Segoe UI");

            const uint child = Native.Constants.WS_CHILD | Native.Constants.WS_VISIBLE;
            const uint tabStop = child | Native.Constants.WS_TABSTOP;
            WriteItem(writer, child, 10, 10, 104, 8, -1, "STATIC", Resources.AWAKE_EXPIRATION_DIALOG_LABEL);
            WriteItem(writer, tabStop | Native.Constants.DTS_SHORTDATEFORMAT, 10, 22, 60, 14, DatePickerId, DateTimePickerClass, string.Empty);
            WriteItem(writer, tabStop | Native.Constants.DTS_TIMEFORMAT, 74, 22, 40, 14, TimePickerId, DateTimePickerClass, string.Empty);
            WriteItem(writer, tabStop | Native.Constants.BS_DEFPUSHBUTTON, 10, ButtonStripTop + 7, 50, 14, Native.Constants.IDOK, "BUTTON", Resources.AWAKE_EXPIRATION_DIALOG_OK);
            WriteItem(writer, tabStop, 64, ButtonStripTop + 7, 50, 14, Native.Constants.IDCANCEL, "BUTTON", Resources.AWAKE_EXPIRATION_DIALOG_CANCEL);

            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteItem(BinaryWriter writer, uint style, short x, short y, short cx, short cy, int id, string windowClass, string text)
        {
            // Each item template has to start on a DWORD boundary.
            while (writer.BaseStream.Position % 4 != 0)
            {
                writer.Write((byte)0);
            }

            writer.Write(style);
            writer.Write(0u); // extended style
            WriteRect(writer, x, y, cx, cy);
            writer.Write((ushort)id);
            WriteString(writer, windowClass);
            WriteString(writer, text);
            writer.Write((short)0); // no creation data
        }

        private static void WriteRect(BinaryWriter writer, short x, short y, short cx, short cy)
        {
            writer.Write(x);
            writer.Write(y);
            writer.Write(cx);
            writer.Write(cy);
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            writer.Write(Encoding.Unicode.GetBytes(value));
            writer.Write((short)0);
        }
    }
}
