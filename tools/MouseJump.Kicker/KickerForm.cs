// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using MouseJump.Kicker.NativeMethods;

using static MouseJump.Kicker.NativeMethods.Core;
using static MouseJump.Kicker.NativeMethods.Kernel32;
using static MouseJump.Kicker.NativeMethods.User32;

namespace MouseJump.Kicker;

public partial class KickerForm : Form
{
    public KickerForm()
    {
        InitializeComponent();
    }

    private Process? Process
    {
        get;
        set;
    }

    private void StartWinUI3_Click(object sender, EventArgs e)
    {
        // this project builds into "{RepoRoot}\tools\MouseJump.Kicker\bin\x64\Debug"
        var kickerDir = AppContext.BaseDirectory;

        // step up to the repo root
        var repoRoot = Path.Combine(kickerDir, "..", "..", "..", "..", "..");

        // and back down to the mouse jump exe
        var mouseJumpExe = Path.Combine(repoRoot, "x64", "Debug", "WinUI3Apps", "PowerToys.MouseJump.WinUI3.exe");

        this.Process = this.StartMouseJump(mouseJumpExe);
    }

    private Process StartMouseJump(string mouseJumpExe)
    {
        if (!File.Exists(mouseJumpExe))
        {
            throw new FileNotFoundException($"Could not find {mouseJumpExe}");
        }

        var args = new List<string>
        {
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
        };
        return Process.Start(mouseJumpExe, args);
    }

    private void ActivationHotkey_Click(object sender, EventArgs e)
    {
        // find the winui3 window and call SetForegroundWindow on it before signalling
        // the event. this mirrors the approach used in on_hotkey() in
        // src/modules/MouseUtils/MouseJump/dllmain.cpp - see the comment there for a
        // full explanation.
        //
        // in brief: the kicker is the foreground process right now (the user just
        // clicked the button), so it has permission to call SetForegroundWindow and
        // transfer foreground status directly to the winui3 window. doing this before
        // signalling the event means that by the time ShowWindowAsync() calls
        // SetForegroundWindow on its own window, the winui3 process already owns the
        // foreground and the call succeeds.
        //
        // using AllowSetForegroundWindow here instead would be unreliable — the
        // permission it grants is revoked as soon as the user generates any input
        // (e.g. moving the mouse), which is almost certain to happen while the
        // preview is rendering.
        if (this.Process is not null)
        {
            var targetPid = (uint)this.Process.Id;
            var winUI3Hwnd = HWND.Null;
            EnumWindows(
                (hwnd, _) =>
                {
                    GetWindowThreadProcessId(hwnd, out var windowPid);
                    if (windowPid == targetPid)
                    {
                        winUI3Hwnd = hwnd;
                        return false; // stop enumeration
                    }

                    return true; // continue enumeration
                },
                LPARAM.Zero);
            if (!winUI3Hwnd.IsNull)
            {
                SetForegroundWindow(winUI3Hwnd);
            }
        }

        const string MOUSE_JUMP_SHOW_PREVIEW_EVENT = "Local\\MouseJumpEvent-aa0be051-3396-4976-b7ba-1a9cc7d236a5";
        var attributes = new LPSECURITY_ATTRIBUTES(
            new SECURITY_ATTRIBUTES(
                (uint)Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
                LPVOID.Null,
                false));
        var hEvent = Kernel32.CreateEventW(
            attributes,
            false,
            false,
            MOUSE_JUMP_SHOW_PREVIEW_EVENT);
        if (hEvent.IsNull)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var result = Kernel32.SetEvent(hEvent);
        if (!result)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private void CloseMouseJump_Click(object sender, EventArgs e)
    {
        this.Process?.Kill();
    }
}
