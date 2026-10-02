// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Awake.Core.Models;
using Awake.Core.Native;
using Awake.Properties;
using ManagedCommon;

namespace Awake.Core
{
    /// <summary>
    /// Lets the user pick the expiration date and time straight from the tray menu.
    /// </summary>
    /// <remarks>
    /// Awake has no UI framework, so the dialog is built from an in-memory template. That keeps
    /// keyboard navigation, DPI scaling and the regional date and time formats of the native pickers.
    /// </remarks>
    internal static class ExpirationDialog
    {
        private const int DatePickerId = 101;
        private const int TimePickerId = 102;
        private const string DateTimePickerClass = "SysDateTimePick32";

        // In dialog units.
        private const short ButtonStripTop = 46;

        // Kept in fields so the delegates are not collected while the dialog is open.
        private static readonly Bridge.DialogProcDelegate DialogProcInstance = DialogProc;
        private static readonly Bridge.SubclassProcDelegate PickerSubclassInstance = PickerSubclassProc;

        private static IntPtr _dialogHandle;
        private static int _wheelDelta;
        private static DateTimeOffset _initialValue;
        private static DateTimeOffset _selectedValue;

        internal static bool TryPick(IntPtr owner, DateTimeOffset initialValue, out DateTimeOffset expireAt)
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

            _initialValue = initialValue;
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
                    expireAt = _selectedValue;
                    return true;
                }

                return false;
            }
            finally
            {
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
                    return 1;

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
                            DateTimeOffset expireAt = ReadPickers(hDlg);
                            if (expireAt <= DateTimeOffset.Now)
                            {
                                _ = Bridge.MessageBox(hDlg, Resources.AWAKE_EXPIRATION_DIALOG_PAST, Constants.FullAppName, Native.Constants.MB_OK | Native.Constants.MB_ICONWARNING);
                                return 1;
                            }

                            _selectedValue = expireAt;
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

            // Past dates can never be a valid expiration, so the calendar does not offer them.
            // The maximum keeps stepping and converting the date clear of DateTime.MaxValue.
            SystemTime[] range = [SystemTime.FromDateTime(DateTime.Today), SystemTime.FromDateTime(new DateTime(9998, 12, 31))];
            Bridge.SendMessage(datePicker, Native.Constants.DTM_SETRANGE, Native.Constants.GDTR_MIN | Native.Constants.GDTR_MAX, range);

            // The calendar button keeps its own gap to the text, the up-down control does not.
            SetPaddedFormat(datePicker, Native.Constants.LOCALE_SSHORTDATE, padEnd: false);

            // The short time format, because the picker's default one includes seconds.
            SetPaddedFormat(timePicker, Native.Constants.LOCALE_SSHORTTIME, padEnd: true);

            // Sized with a wide value: two-digit day, month and hour, in the afternoon.
            // It is next year's so that it lies within the allowed range.
            SystemTime widest = SystemTime.FromDateTime(new DateTime(DateTime.Today.Year + 1, 12, 28, 22, 58, 0));
            Bridge.SendMessage(datePicker, Native.Constants.DTM_SETSYSTEMTIME, Native.Constants.GDT_VALID, ref widest);
            Bridge.SendMessage(timePicker, Native.Constants.DTM_SETSYSTEMTIME, Native.Constants.GDT_VALID, ref widest);
            FitPickers(hDlg, datePicker, timePicker);

            SystemTime initial = SystemTime.FromDateTime(_initialValue.LocalDateTime);
            Bridge.SendMessage(datePicker, Native.Constants.DTM_SETSYSTEMTIME, Native.Constants.GDT_VALID, ref initial);
            Bridge.SendMessage(timePicker, Native.Constants.DTM_SETSYSTEMTIME, Native.Constants.GDT_VALID, ref initial);

            Bridge.SetWindowSubclass(datePicker, PickerSubclassInstance, DatePickerId, 0);
            Bridge.SetWindowSubclass(timePicker, PickerSubclassInstance, TimePickerId, 0);
        }

        // The regional formats differ in length, so the pickers take the width their format needs
        // and the dialog only grows when they do not fit.
        private static void FitPickers(IntPtr hDlg, IntPtr datePicker, IntPtr timePicker)
        {
            Size dateSize = default;
            Size timeSize = default;
            Bridge.SendMessage(datePicker, Native.Constants.DTM_GETIDEALSIZE, 0, ref dateSize);
            Bridge.SendMessage(timePicker, Native.Constants.DTM_GETIDEALSIZE, 0, ref timeSize);

            Rect date = PositionInDialog(hDlg, datePicker);
            Rect gap = new() { Right = 4 };
            Bridge.MapDialogRect(hDlg, ref gap);

            int height = date.Bottom - date.Top;
            int timeLeft = date.Left + dateSize.Width + gap.Right;
            Bridge.SetWindowPos(datePicker, IntPtr.Zero, date.Left, date.Top, dateSize.Width, height, Native.Constants.SWP_NOZORDER | Native.Constants.SWP_NOACTIVATE);
            Bridge.SetWindowPos(timePicker, IntPtr.Zero, timeLeft, date.Top, timeSize.Width, height, Native.Constants.SWP_NOZORDER | Native.Constants.SWP_NOACTIVATE);

            Bridge.GetClientRect(hDlg, out Rect client);
            int overflow = timeLeft + timeSize.Width + date.Left - client.Right;
            if (overflow <= 0)
            {
                return;
            }

            Bridge.GetWindowRect(hDlg, out Rect window);
            Bridge.SetWindowPos(hDlg, IntPtr.Zero, window.Left - (overflow / 2), window.Top, window.Right - window.Left + overflow, window.Bottom - window.Top, Native.Constants.SWP_NOZORDER | Native.Constants.SWP_NOACTIVATE);

            foreach (int buttonId in new[] { Native.Constants.IDOK, Native.Constants.IDCANCEL })
            {
                IntPtr button = Bridge.GetDlgItem(hDlg, buttonId);
                Rect position = PositionInDialog(hDlg, button);
                Bridge.SetWindowPos(button, IntPtr.Zero, position.Left + overflow, position.Top, 0, 0, Native.Constants.SWP_NOZORDER | Native.Constants.SWP_NOACTIVATE | Native.Constants.SWP_NOSIZE);
            }
        }

        private static Rect PositionInDialog(IntPtr hDlg, IntPtr control)
        {
            Bridge.GetWindowRect(control, out Rect rect);
            _ = Bridge.MapWindowPoints(IntPtr.Zero, hDlg, ref rect, 2);
            return rect;
        }

        // The picker has no padding setting and draws its text against the border, so the
        // format gets en spaces; a regular space is too narrow to be noticed.
        private static void SetPaddedFormat(IntPtr picker, uint localeFormat, bool padEnd)
        {
            char[] format = new char[80];
            int length = Bridge.GetLocaleInfoEx(null, localeFormat, format, format.Length);
            if (length > 1)
            {
                Bridge.SendMessage(picker, Native.Constants.DTM_SETFORMAT, 0, "\u2002" + new string(format, 0, length - 1) + (padEnd ? "\u2002" : string.Empty));
            }
        }

        // The native pickers ignore the mouse wheel.
        private static IntPtr PickerSubclassProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData)
        {
            switch (message)
            {
                case Native.Constants.WM_MOUSEWHEEL:
                    if (Bridge.GetFocus() != hWnd)
                    {
                        Bridge.SendMessage(_dialogHandle, Native.Constants.WM_NEXTDLGCTL, (nuint)hWnd, 1);
                        _wheelDelta = 0;
                    }

                    // Touchpads report fractions of a notch, so partial deltas add up until a full step.
                    _wheelDelta += (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    int steps = _wheelDelta / Native.Constants.WHEEL_DELTA;
                    _wheelDelta %= Native.Constants.WHEEL_DELTA;

                    if (uIdSubclass == DatePickerId)
                    {
                        StepDate(hWnd, steps);
                    }
                    else
                    {
                        for (int i = 0; i < Math.Abs(steps); i++)
                        {
                            Bridge.SendMessage(hWnd, Native.Constants.WM_KEYDOWN, steps > 0 ? Native.Constants.VK_UP : Native.Constants.VK_DOWN, 0);
                        }
                    }

                    return 0;

                case Native.Constants.WM_NCDESTROY:
                    Bridge.RemoveWindowSubclass(hWnd, PickerSubclassInstance, uIdSubclass);
                    break;
            }

            return Bridge.DefSubclassProc(hWnd, message, wParam, lParam);
        }

        // Whole days rather than the selected field: the picker wraps a field within its month,
        // so on the 30th the day field could never reach tomorrow.
        private static void StepDate(IntPtr datePicker, int days)
        {
            if (days == 0)
            {
                return;
            }

            SystemTime current = default;
            Bridge.SendMessage(datePicker, Native.Constants.DTM_GETSYSTEMTIME, 0, ref current);

            DateTime stepped = new DateTime(current.Year, current.Month, current.Day).AddDays(days);
            SystemTime value = SystemTime.FromDateTime(stepped < DateTime.Today ? DateTime.Today : stepped);
            Bridge.SendMessage(datePicker, Native.Constants.DTM_SETSYSTEMTIME, Native.Constants.GDT_VALID, ref value);
        }

        // White content area above a button strip, like the Windows message boxes.
        private static void PaintBackground(IntPtr hDlg, IntPtr hdc)
        {
            Bridge.GetClientRect(hDlg, out Rect client);

            Rect strip = new() { Top = ButtonStripTop };
            Bridge.MapDialogRect(hDlg, ref strip);

            Rect content = client with { Bottom = strip.Top };
            Rect buttonStrip = client with { Top = strip.Top };
            _ = Bridge.FillRect(hdc, ref content, Bridge.GetSysColorBrush(Native.Constants.COLOR_WINDOW));
            _ = Bridge.FillRect(hdc, ref buttonStrip, Bridge.GetSysColorBrush(Native.Constants.COLOR_BTNFACE));
        }

        private static DateTimeOffset ReadPickers(IntPtr hDlg)
        {
            SystemTime date = default;
            SystemTime time = default;
            Bridge.SendMessage(Bridge.GetDlgItem(hDlg, DatePickerId), Native.Constants.DTM_GETSYSTEMTIME, 0, ref date);
            Bridge.SendMessage(Bridge.GetDlgItem(hDlg, TimePickerId), Native.Constants.DTM_GETSYSTEMTIME, 0, ref time);

            return new DateTimeOffset(new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0, DateTimeKind.Local));
        }

        // Layout is in dialog units, which Windows scales with the font and the monitor DPI.
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
