// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace MonitorPower;

internal readonly record struct ControllerChord(ushort Buttons)
{
    private static readonly IReadOnlyDictionary<string, ushort> ButtonNames =
        new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            ["DPadUp"] = 0x0001,
            ["DPadDown"] = 0x0002,
            ["DPadLeft"] = 0x0004,
            ["DPadRight"] = 0x0008,
            ["Start"] = 0x0010,
            ["View"] = 0x0020,
            ["Back"] = 0x0020,
            ["LeftThumb"] = 0x0040,
            ["RightThumb"] = 0x0080,
            ["LeftShoulder"] = 0x0100,
            ["RightShoulder"] = 0x0200,
            ["Guide"] = 0x0400,
            ["A"] = 0x1000,
            ["B"] = 0x2000,
            ["X"] = 0x4000,
            ["Y"] = 0x8000,
        };

    public static bool TryParse(string? value, out ControllerChord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !ButtonNames.TryGetValue(parts[0], out var first) ||
            !ButtonNames.TryGetValue(parts[1], out var second) || first == second)
        {
            return false;
        }

        chord = new ControllerChord((ushort)(first | second));
        return true;
    }

    public bool IsPressed(ushort buttons) => (buttons & Buttons) == Buttons;
}
