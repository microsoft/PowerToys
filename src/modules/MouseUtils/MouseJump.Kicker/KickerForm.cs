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

    private void StartWinForms_Click(object sender, EventArgs e)
    {
        var baseDir = AppContext.BaseDirectory;
        var exePath = Path.Combine(baseDir, "PowerToys.MouseJumpUI.exe");
        this.Process = this.StartProcess(exePath);
    }

    private void StartWinUI3_Click(object sender, EventArgs e)
    {
        var baseDir = AppContext.BaseDirectory;
        var exePath = Path.Combine(baseDir, "WinUI3Apps", "PowerToys.MouseJump.WinUI3.exe");
        this.Process = this.StartProcess(exePath);
    }

    private Process StartProcess(string exePath)
    {
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"Could not find {exePath}");
        }

        var args = new List<string>
        {
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
        };
        return Process.Start(exePath, args);
    }

    private void ActivationHotkey_Click(object sender, EventArgs e)
    {
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
