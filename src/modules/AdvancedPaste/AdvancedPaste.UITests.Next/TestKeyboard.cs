// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;

namespace AdvancedPaste.UITests;

internal static class TestKeyboard
{
    internal static void SendChord(params Key[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentOutOfRangeException.ThrowIfZero(keys.Length);
        var inputs = new Input[keys.Length * 2];
        for (var index = 0; index < keys.Length; index++)
        {
            inputs[index] = CreateInput(keys[index], keyUp: false);
            inputs[keys.Length + index] = CreateInput(keys[keys.Length - index - 1], keyUp: true);
        }

        // Queue the complete chord atomically. Releasing a SendKeys modifier after the module
        // starts its own Ctrl+V can otherwise turn the product's paste into a literal 'v'.
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            var error = Marshal.GetLastWin32Error();
            foreach (var key in keys.Reverse())
            {
                KeyboardHelper.ReleaseKey(key);
            }

            throw new Win32Exception(error, $"Only {sent} of {inputs.Length} keyboard events were injected.");
        }
    }

    private static Input CreateInput(Key key, bool keyUp)
    {
        var extended = key is Key.LWin or Key.RCtrl or Key.Left or Key.Up or Key.Right or Key.Down or
            Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Insert or Key.Delete;
        return new Input
        {
            Type = 1,
            Data = new InputData
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = (ushort)key,
                    Flags = (keyUp ? 2U : 0U) | (extended ? 1U : 0U),
                },
            },
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        internal uint Type;
        internal InputData Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputData
    {
        [FieldOffset(0)]
        internal KeyboardInput Keyboard;

        [FieldOffset(0)]
        internal MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        internal ushort VirtualKey;
        internal ushort ScanCode;
        internal uint Flags;
        internal uint Time;
        internal UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        internal int X;
        internal int Y;
        internal uint MouseData;
        internal uint Flags;
        internal uint Time;
        internal UIntPtr ExtraInfo;
    }
}
