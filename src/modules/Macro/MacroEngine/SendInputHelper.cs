// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace PowerToys.MacroEngine;

internal sealed class SendInputHelper : ISendInputHelper
{
    private static readonly int InputSize = Marshal.SizeOf<INPUT>();

    public void PressKeyCombo(string combo)
    {
        var (modifierKeys, mainVk) = KeyParser.ParseKeyCombo(combo);
        var inputs = new List<INPUT>();

        foreach (var mod in modifierKeys)
        {
            inputs.Add(KeyDown(mod));
        }

        inputs.Add(KeyDown(mainVk));
        inputs.Add(KeyUp(mainVk));

        foreach (var mod in Enumerable.Reverse(modifierKeys))
        {
            inputs.Add(KeyUp(mod));
        }

        Send(inputs);
    }

    public void TypeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var inputs = new List<INPUT>(text.Length * 2);
        foreach (char c in text)
        {
            inputs.Add(UnicodeKeyDown(c));
            inputs.Add(UnicodeKeyUp(c));
        }

        Send(inputs);
    }

    private static unsafe void Send(List<INPUT> inputs)
    {
        var span = CollectionsMarshal.AsSpan(inputs);
        uint sent = PInvoke.SendInput(span, InputSize);
        if (sent < (uint)inputs.Count)
        {
            System.Diagnostics.Trace.WriteLine($"[MacroEngine] SendInput: {sent}/{inputs.Count} events injected.");
        }
    }

    private static INPUT KeyDown(ushort vk) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous = new() { ki = new KEYBDINPUT { wVk = (VIRTUAL_KEY)vk } },
    };

    private static INPUT KeyUp(ushort vk) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous = new() { ki = new KEYBDINPUT { wVk = (VIRTUAL_KEY)vk, dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP } },
    };

    private static INPUT UnicodeKeyDown(char c) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous = new() { ki = new KEYBDINPUT { wScan = c, dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE } },
    };

    private static INPUT UnicodeKeyUp(char c) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous = new() { ki = new KEYBDINPUT { wScan = c, dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP } },
    };
}
