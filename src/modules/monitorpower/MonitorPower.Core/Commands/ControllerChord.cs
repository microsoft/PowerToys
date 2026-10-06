// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MonitorPower;

internal readonly record struct ControllerChord(ushort Buttons)
{
    private static readonly KeyValuePair<string, ushort>[] ButtonNames =
    [
        new("DPadUp", 0x0001),
        new("DPadDown", 0x0002),
        new("DPadLeft", 0x0004),
        new("DPadRight", 0x0008),
        new("Start", 0x0010),
        new("View", 0x0020),
        new("LeftThumb", 0x0040),
        new("RightThumb", 0x0080),
        new("LeftShoulder", 0x0100),
        new("RightShoulder", 0x0200),
        new("A", 0x1000),
        new("B", 0x2000),
        new("X", 0x4000),
        new("Y", 0x8000),
    ];

    public static bool TryParse(string? value, out ControllerChord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !TryGetButton(parts[0], out var first) ||
            !TryGetButton(parts[1], out var second) ||
            first == second)
        {
            return false;
        }

        chord = new ControllerChord((ushort)(first | second));
        return true;
    }

    public static bool TryCapture(ushort buttons, out string chord)
    {
        var pressedButtons = ButtonNames
            .Where(button => (buttons & button.Value) != 0)
            .ToArray();
        if (pressedButtons.Length != 2)
        {
            chord = string.Empty;
            return false;
        }

        chord = $"{pressedButtons[0].Key} + {pressedButtons[1].Key}";
        return true;
    }

    public static string DescribeButtons(ushort buttons)
    {
        var pressed = ButtonNames
            .Where(button => (buttons & button.Value) != 0)
            .Select(button => button.Key)
            .ToArray();
        return pressed.Length == 0 ? "No buttons pressed" : string.Join(" + ", pressed);
    }

    public bool IsPressed(ushort buttons) => (buttons & Buttons) == Buttons;

    private static bool TryGetButton(string value, out ushort button)
    {
        foreach (var name in ButtonNames)
        {
            if (string.Equals(name.Key, value, StringComparison.OrdinalIgnoreCase))
            {
                button = name.Value;
                return true;
            }
        }

        button = 0;
        return false;
    }
}
