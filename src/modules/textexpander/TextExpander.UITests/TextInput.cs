// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// Types text the way a keyboard does: one key press per character, with Shift when the layout needs it, through
/// <c>SendInput</c>. The engine's keyboard hook decodes these keys with the layout of the window in front, as it does
/// for a physical keyboard. A character the layout can't type goes as a Unicode packet, as the touch keyboard sends it.
/// </summary>
internal static class TextInput
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint MapVirtualKeyToScanCode = 0;
    private const ushort VirtualKeyShift = 0x10;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyMenu = 0x12;
    private const ushort VirtualKeyReturn = 0x0D;
    private const ushort VirtualKeyTab = 0x09;

    /// <summary>Types <paramref name="text"/> into the window in front, <paramref name="delayMS"/> apart.</summary>
    public static void Type(string text, int delayMS = 40)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (char ch in text)
        {
            TypeCharacter(ch);
            Thread.Sleep(delayMS);
        }
    }

    /// <summary>Presses and releases one key, with the modifiers in <paramref name="modifiers"/> held around it.</summary>
    public static void Press(ushort virtualKey, params ushort[] modifiers)
    {
        var inputs = new List<Input>();
        foreach (var modifier in modifiers)
        {
            inputs.Add(KeyInput(modifier, keyUp: false));
        }

        inputs.Add(KeyInput(virtualKey, keyUp: false));
        inputs.Add(KeyInput(virtualKey, keyUp: true));
        for (var index = modifiers.Length - 1; index >= 0; index--)
        {
            inputs.Add(KeyInput(modifiers[index], keyUp: true));
        }

        Send(inputs);
    }

    public static void Enter() => Press(VirtualKeyReturn);

    public static void Escape() => Press(0x1B);

    /// <summary>Ctrl+Z, sent as one batch so nothing comes between the keys.</summary>
    public static void Undo() => Press(0x5A, VirtualKeyControl);

    /// <summary>Ctrl+A.</summary>
    public static void SelectAll() => Press(0x41, VirtualKeyControl);

    /// <summary>Releases modifiers a failed test may have left down, so the next test types plain keys.</summary>
    public static void ReleaseModifiers()
    {
        var inputs = new List<Input>();
        foreach (var key in new ushort[] { VirtualKeyShift, 0xA0, 0xA1, VirtualKeyControl, 0xA2, 0xA3, VirtualKeyMenu, 0xA4, 0xA5, 0x5B, 0x5C })
        {
            if ((GetAsyncKeyState(key) & 0x8000) != 0)
            {
                inputs.Add(KeyInput(key, keyUp: true));
            }
        }

        if (inputs.Count > 0)
        {
            Send(inputs);
        }
    }

    private static void TypeCharacter(char ch)
    {
        switch (ch)
        {
            case '\n':
                Press(VirtualKeyReturn);
                return;
            case '\t':
                Press(VirtualKeyTab);
                return;
        }

        var layout = GetKeyboardLayout(ForegroundThreadId());
        short scan = VkKeyScanExW(ch, layout);
        var virtualKey = (ushort)(scan & 0xFF);
        var shiftState = (scan >> 8) & 0xFF;

        // Not on this layout, or only with Ctrl or Alt (AltGr): send the character itself.
        if (scan == -1 || (shiftState & 0x06) != 0)
        {
            Send(new List<Input> { UnicodeInput(ch, keyUp: false), UnicodeInput(ch, keyUp: true) });
            return;
        }

        if ((shiftState & 0x01) != 0)
        {
            Press(virtualKey, VirtualKeyShift);
        }
        else
        {
            Press(virtualKey);
        }
    }

    private static uint ForegroundThreadId()
    {
        var foreground = GetForegroundWindow();
        return foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out _);
    }

    private static Input KeyInput(ushort virtualKey, bool keyUp)
    {
        var layout = GetKeyboardLayout(ForegroundThreadId());
        return new Input
        {
            Type = InputKeyboard,
            Data = new InputData
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    ScanCode = (ushort)MapVirtualKeyExW(virtualKey, MapVirtualKeyToScanCode, layout),
                    Flags = (keyUp ? KeyEventKeyUp : 0) | (IsExtended(virtualKey) ? KeyEventExtendedKey : 0),
                },
            },
        };
    }

    private static Input UnicodeInput(char ch, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Data = new InputData
        {
            Keyboard = new KeyboardInput
            {
                ScanCode = ch,
                Flags = KeyEventUnicode | (keyUp ? KeyEventKeyUp : 0),
            },
        },
    };

    // Arrows, Home/End, Page Up/Down, Insert/Delete and the right-hand modifiers are extended keys.
    private static bool IsExtended(ushort virtualKey) =>
        virtualKey is >= 0x21 and <= 0x28 or 0x2D or 0x2E or 0x5B or 0x5C or 0xA3 or 0xA5;

    private static void Send(List<Input> inputs)
    {
        var batch = inputs.ToArray();
        var sent = SendInput((uint)batch.Length, batch, Marshal.SizeOf<Input>());
        if (sent != batch.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Only {sent} of {batch.Length} keyboard events were injected.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern short VkKeyScanExW(char ch, IntPtr layout);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint MapVirtualKeyExW(uint code, uint mapType, IntPtr layout);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputData Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputData
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        // MOUSEINPUT is the largest member and sets the native INPUT size.
        [FieldOffset(0)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}
