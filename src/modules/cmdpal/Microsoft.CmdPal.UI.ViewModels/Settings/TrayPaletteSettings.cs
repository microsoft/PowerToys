// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

public sealed record TrayPaletteSettings
{
    private ImmutableList<PinnedCommandSettings> _commands =
    [
        new("com.microsoft.cmdpal.builtin.windowssettings", "ms-settings:network"),
        new("com.microsoft.cmdpal.builtin.windowssettings", "ms-settings:bluetooth"),
        new("AllApps", "aumid:Microsoft.ScreenSketch_8wekyb3d8bbwe!App"),
    ];

    public ImmutableList<PinnedCommandSettings> Commands
    {
        get => _commands;
        init => _commands = value ?? [];
    }

    public TrayPaletteSettings Pin(PinnedCommandSettings pin) =>
        Commands.Contains(pin) ? this : this with { Commands = Commands.Add(pin) };

    public TrayPaletteSettings Unpin(PinnedCommandSettings pin) =>
        this with { Commands = Commands.Remove(pin) };

    public TrayPaletteSettings Reorder(IReadOnlyList<PinnedCommandSettings> visibleOrder)
    {
        // Replace only visible slots, so unavailable extensions keep their saved positions.
        var visible = visibleOrder.ToHashSet();
        if (visible.Count != visibleOrder.Count || visible.Any(pin => !Commands.Contains(pin)))
        {
            throw new ArgumentException("The order must contain distinct, saved pins.", nameof(visibleOrder));
        }

        var next = 0;
        return this with
        {
            Commands = Commands.Select(pin => visible.Contains(pin) ? visibleOrder[next++] : pin).ToImmutableList(),
        };
    }
}
