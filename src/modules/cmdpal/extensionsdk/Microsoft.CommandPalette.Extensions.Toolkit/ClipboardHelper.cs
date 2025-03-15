// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

// shamelessly from https://github.com/PowerShell/PowerShell/blob/master/src/Microsoft.PowerShell.Commands.Management/commands/management/Clipboard.cs
public static partial class ClipboardHelper
{
    private static readonly bool? _clipboardSupported = true;

    // Used if an external clipboard is not available, e.g. if xclip is missing.
    // This is useful for testing in CI as well.
    private static string? _internalClipboard;

    public static string GetText()
    {
        if (_clipboardSupported == false)
        {
            return _internalClipboard ?? string.Empty;
        }

        var tool = string.Empty;
        var args = string.Empty;
        var clipboardText = string.Empty;

        ExecuteOnStaThread(() => GetTextImpl(out clipboardText));
        return clipboardText;
    }

    public static void SetText(string text)
    {
        if (_clipboardSupported == false)
        {
            _internalClipboard = text;
            return;
        }

        var tool = string.Empty;
        var args = string.Empty;
        ExecuteOnStaThread(() => SetClipboardData(Tuple.Create(text, CF_UNICODETEXT)));
        return;
    }

    public static void SetRtf(string plainText, string rtfText)
    {
        if (s_CF_RTF == 0)
        {
            s_CF_RTF = RegisterClipboardFormat("Rich Text Format");
        }

        ExecuteOnStaThread(() => SetClipboardData(
            Tuple.Create(plainText, CF_UNICODETEXT),
            Tuple.Create(rtfText, s_CF_RTF)));
    }

#pragma warning disable SA1310 // Field names should not contain underscore
    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint GMEM_ZEROINIT = 0x0040;
#pragma warning restore SA1310 // Field names should not contain underscore
    private const uint GHND = GMEM_MOVEABLE | GMEM_ZEROINIT;

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GlobalAlloc(uint flags, UIntPtr dwBytes);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GlobalFree(IntPtr hMem);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GlobalLock(IntPtr hMem);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(IntPtr hMem);

    [LibraryImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
    private static partial void CopyMemory(IntPtr dest, IntPtr src, uint count);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsClipboardFormatAvailable(uint format);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(IntPtr hWndNewOwner);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetClipboardData(uint format);

    [LibraryImport("user32.dll")]
    private static partial IntPtr SetClipboardData(uint format, IntPtr data);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterClipboardFormat(string lpszFormat);

#pragma warning disable SA1310 // Field names should not contain underscore
    private const uint CF_TEXT = 1;
    private const uint CF_UNICODETEXT = 13;

#pragma warning disable SA1308 // Variable names should not be prefixed
    private static uint s_CF_RTF;
#pragma warning restore SA1308 // Variable names should not be prefixed
#pragma warning restore SA1310 // Field names should not contain underscore

    private static bool GetTextImpl(out string text)
    {
        try
        {
            if (IsClipboardFormatAvailable(CF_UNICODETEXT))
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    var data = GetClipboardData(CF_UNICODETEXT);
                    if (data != IntPtr.Zero)
                    {
                        data = GlobalLock(data);
                        text = Marshal.PtrToStringUni(data) ?? string.Empty;
                        GlobalUnlock(data);
                        return true;
                    }
                }
            }
            else if (IsClipboardFormatAvailable(CF_TEXT))
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    var data = GetClipboardData(CF_TEXT);
                    if (data != IntPtr.Zero)
                    {
                        data = GlobalLock(data);
                        text = Marshal.PtrToStringAnsi(data) ?? string.Empty;
                        GlobalUnlock(data);
                        return true;
                    }
                }
            }
        }
        catch
        {
            // Ignore exceptions
        }
        finally
        {
            CloseClipboard();
        }

        text = string.Empty;
        return false;
    }

    private static bool SetClipboardData(params Tuple<string, uint>[] data)
    {
        try
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                return false;
            }

            EmptyClipboard();

            foreach (var d in data)
            {
                if (!SetSingleClipboardData(d.Item1, d.Item2))
                {
                    return false;
                }
            }
        }
        finally
        {
            CloseClipboard();
        }

        return true;
    }

    private static bool SetSingleClipboardData(string text, uint format)
    {
        var hGlobal = IntPtr.Zero;
        var data = IntPtr.Zero;

        try
        {
            uint bytes;
            if (format == s_CF_RTF || format == CF_TEXT)
            {
                bytes = (uint)(text.Length + 1);
                data = Marshal.StringToHGlobalAnsi(text);
            }
            else if (format == CF_UNICODETEXT)
            {
                bytes = (uint)((text.Length + 1) * 2);
                data = Marshal.StringToHGlobalUni(text);
            }
            else
            {
                // Not yet supported format.
                return false;
            }

            if (data == IntPtr.Zero)
            {
                return false;
            }

            hGlobal = GlobalAlloc(GHND, (UIntPtr)bytes);
            if (hGlobal == IntPtr.Zero)
            {
                return false;
            }

            var dataCopy = GlobalLock(hGlobal);
            if (dataCopy == IntPtr.Zero)
            {
                return false;
            }

            CopyMemory(dataCopy, data, bytes);
            GlobalUnlock(hGlobal);

            if (SetClipboardData(format, hGlobal) != IntPtr.Zero)
            {
                // The clipboard owns this memory now, so don't free it.
                hGlobal = IntPtr.Zero;
            }
        }
        catch
        {
            // Ignore failures
        }
        finally
        {
            if (data != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(data);
            }

            if (hGlobal != IntPtr.Zero)
            {
                GlobalFree(hGlobal);
            }
        }

        return true;
    }

    private static void ExecuteOnStaThread(Func<bool> action)
    {
        const int RetryCount = 5;
        var tries = 0;

        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            while (tries++ < RetryCount && !action())
            {
                // wait until RetryCount or action
            }

            return;
        }

        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                while (tries++ < RetryCount && !action())
                {
                    // wait until RetryCount or action
                }
            }
            catch (Exception e)
            {
                exception = e;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            throw exception;
        }
    }
}
