// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Messages;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.Insertion;

/// <summary>The real windows and keyboard.</summary>
internal sealed class Win32Desktop : IDesktop
{
    /// <summary>
    /// The tag on every key event sent from here (<see cref="InputTags.CommandPalette"/>): Keyboard Manager's injected
    /// bit, so its remaps leave the paste alone, in a value Text Expander's hook ignores, so a snippet's typed text can't
    /// set off one of its triggers.
    /// </summary>
    internal const nuint InputTag = InputTags.CommandPalette;

    /// <summary>Windows' medium integrity level (<c>SECURITY_MANDATORY_MEDIUM_RID</c>), the highest a snippet goes to.</summary>
    private const uint MediumIntegrity = 0x2000;

    private const uint ResponsiveProbeMs = 50;

    private readonly Lazy<uint?> _ownIntegrity = new(() => IntegrityLevelOf(NativeMethods.GetCurrentProcess()));

    public nint ForegroundWindow => NativeMethods.GetForegroundWindow();

    public uint CurrentProcessId => (uint)Environment.ProcessId;

    public IReadOnlyList<ushort> HeldModifiers()
    {
        var held = new List<ushort>();
        foreach (var key in KeyStrokes.Modifiers)
        {
            if ((NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0)
            {
                held.Add(key);
            }
        }

        return held;
    }

    public uint ProcessIdOf(nint window)
    {
        _ = NativeMethods.GetWindowThreadProcessId(window, out var processId);
        return processId;
    }

    public bool CanDrive(nint window)
    {
        if (window == 0 || _ownIntegrity.Value is not uint ours)
        {
            return false;
        }

        var processId = ProcessIdOf(window);
        if (processId == 0)
        {
            return false;
        }

        // UIPI drops input travelling up, and a snippet the user could have planted isn't sent to an elevated window
        // even from an elevated Command Palette. A process that can't be inspected counts as out of reach.
        var ceiling = Math.Min(ours, MediumIntegrity);
        var process = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == 0)
        {
            return false;
        }

        try
        {
            return IntegrityLevelOf(process) is uint theirs && theirs <= ceiling;
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }

    public bool IsTakingMessages(nint window)
        => window == 0
            || !NativeMethods.IsWindow(window)
            || NativeMethods.SendMessageTimeout(window, NativeMethods.WM_NULL, 0, 0, NativeMethods.SMTO_BLOCK | NativeMethods.SMTO_ABORTIFHUNG, ResponsiveProbeMs, out _) != 0;

    public unsafe bool IsWindowsTerminal(nint window)
    {
        if (window == 0)
        {
            return false;
        }

        // One longer than the class name, so a longer name that starts with it doesn't match.
        var length = InjectionPolicy.WindowsTerminalClass.Length + 2;
        var name = stackalloc char[length];
        var written = NativeMethods.GetClassName(window, name, length);
        return written > 0 && InjectionPolicy.IsWindowsTerminalClass(new string(name, 0, written));
    }

    public unsafe bool IsConsole(nint window)
    {
        if (window == 0)
        {
            return false;
        }

        var length = InjectionPolicy.ConsoleWindowClass.Length + 2;
        var name = stackalloc char[length];
        var written = NativeMethods.GetClassName(window, name, length);
        return written > 0 && InjectionPolicy.IsConsoleClass(new string(name, 0, written));
    }

    public bool Send(IReadOnlyList<KeyStroke> strokes)
    {
        ArgumentNullException.ThrowIfNull(strokes);

        if (strokes.Count == 0)
        {
            return true;
        }

        var inputs = new NativeMethods.INPUT[strokes.Count];
        for (var i = 0; i < inputs.Length; i++)
        {
            var stroke = strokes[i];
            var unicode = stroke.Character != '\0';
            inputs[i] = new NativeMethods.INPUT
            {
                Type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    Ki = new NativeMethods.KEYBDINPUT
                    {
                        WVk = unicode ? (ushort)0 : stroke.VirtualKey,
                        WScan = unicode ? stroke.Character : (ushort)0,
                        DwFlags = (unicode ? NativeMethods.KEYEVENTF_UNICODE : 0) | (stroke.Up ? NativeMethods.KEYEVENTF_KEYUP : 0),
                        DwExtraInfo = InputTag,
                    },
                },
            };
        }

        // The count says the events went in, not that they arrived: UIPI drops them silently, hence CanDrive.
        return NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>()) == inputs.Length;
    }

    public void HideCommandPalette() => WeakReferenceMessenger.Default.Send(new HideWindowMessage());

    /// <summary>
    /// Reads a process's integrity level, the last sub-authority of its mandatory label, as Text Expander does
    /// (<c>InputTarget.GetIntegrityLevel</c>). Null when it can't be read.
    /// </summary>
    private static uint? IntegrityLevelOf(nint process)
    {
        if (!NativeMethods.OpenProcessToken(process, NativeMethods.TOKEN_QUERY, out var token))
        {
            return null;
        }

        try
        {
            // Asked for its size first: the label is a SID, whose length varies. This call fails by design.
            _ = NativeMethods.GetTokenInformation(token, NativeMethods.TokenIntegrityLevel, 0, 0, out var size);
            if (size == 0)
            {
                return null;
            }

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (!NativeMethods.GetTokenInformation(token, NativeMethods.TokenIntegrityLevel, buffer, size, out _))
                {
                    return null;
                }

                // TOKEN_MANDATORY_LABEL starts with the label's SID.
                var sid = Marshal.ReadIntPtr(buffer);
                var countPointer = sid == 0 ? 0 : NativeMethods.GetSidSubAuthorityCount(sid);
                if (countPointer == 0)
                {
                    return null;
                }

                var count = Marshal.ReadByte(countPointer);
                var levelPointer = count == 0 ? 0 : NativeMethods.GetSidSubAuthority(sid, (uint)(count - 1));
                return levelPointer == 0 ? null : unchecked((uint)Marshal.ReadInt32(levelPointer));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            NativeMethods.CloseHandle(token);
        }
    }
}
