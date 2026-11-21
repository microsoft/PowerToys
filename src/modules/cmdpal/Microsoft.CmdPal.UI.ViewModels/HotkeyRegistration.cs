// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Immutable snapshot of effective global shortcuts and their requested registration mode.
/// </summary>
/// <remarks>
/// Null and empty shortcuts are excluded, and display text is ignored.
/// Duplicate combinations retain the first binding in activation, dock, then command order.
/// </remarks>
public sealed class HotkeyRegistration
{
    /// <summary>Gets whether the snapshot requests the low-level keyboard hook.</summary>
    public bool UseLowLevelGlobalHotkey { get; }

    /// <summary>Gets normalized key-to-command mappings; an empty command ID summons the palette.</summary>
    public ImmutableDictionary<HotkeySettings, string> Hotkeys { get; }

    /// <summary>Gets rejected bindings mapped to the command that owns their key combination.</summary>
    public ImmutableDictionary<TopLevelHotkey, string> Conflicts { get; }

    /// <summary>Initializes a new instance of the <see cref="HotkeyRegistration"/> class.</summary>
    /// <param name="settings">Activation shortcut, dock shortcut, dock enabled state, and registration mode.</param>
    /// <param name="activeCommandHotkeys">Command bindings already filtered to available commands.</param>
    public HotkeyRegistration(SettingsModel settings, ImmutableList<TopLevelHotkey> activeCommandHotkeys)
    {
        UseLowLevelGlobalHotkey = settings.UseLowLevelGlobalHotkey;
        var hotkeys = ImmutableDictionary.CreateBuilder<HotkeySettings, string>();
        var conflicts = ImmutableDictionary.CreateBuilder<TopLevelHotkey, string>();
        if (!settings.IsWindowsKeyHotkeyActive)
        {
            AddHotkey(settings.Hotkey, string.Empty);
        }

        if (settings.EnableDock)
        {
            AddHotkey(settings.DockFocusHotkey, DockHotkeyIds.FocusDock);
        }

        foreach (var commandHotkey in activeCommandHotkeys)
        {
            AddHotkey(commandHotkey.Hotkey, commandHotkey.CommandId);
        }

        Hotkeys = hotkeys.ToImmutable();
        Conflicts = conflicts.ToImmutable();

        return;

        void AddHotkey(HotkeySettings? hotkey, string commandId)
        {
            if (hotkey is null || hotkey.IsEmpty())
            {
                return;
            }

            // Key is display metadata; registration uses only modifiers and the virtual-key code.
            var key = new HotkeySettings(hotkey.Win, hotkey.Ctrl, hotkey.Alt, hotkey.Shift, hotkey.Code);

            // Both registration paths give the first command using a shortcut priority.
            if (!hotkeys.TryAdd(key, commandId))
            {
                conflicts.TryAdd(new TopLevelHotkey(key, commandId), hotkeys[key]);
            }
        }
    }

    private HotkeyRegistration(
        bool useLowLevelGlobalHotkey,
        ImmutableDictionary<HotkeySettings, string> hotkeys,
        ImmutableDictionary<TopLevelHotkey, string> conflicts)
    {
        UseLowLevelGlobalHotkey = useLowLevelGlobalHotkey;
        Hotkeys = hotkeys;
        Conflicts = conflicts;
    }

    /// <summary>Computes the bindings to remove and add when applying this snapshot.</summary>
    /// <param name="previous">Previous snapshot, or <see langword="null"/> for initial registration.</param>
    /// <returns>Removed and added bindings; changing registration mode replaces every binding.</returns>
    public (ImmutableDictionary<HotkeySettings, string> Removed, ImmutableDictionary<HotkeySettings, string> Added) GetChanges(HotkeyRegistration? previous)
    {
        if (previous is null || UseLowLevelGlobalHotkey != previous.UseLowLevelGlobalHotkey)
        {
            return (previous?.Hotkeys ?? ImmutableDictionary<HotkeySettings, string>.Empty, Hotkeys);
        }

        var removed = previous.Hotkeys.Where(hotkey => !Hotkeys.TryGetValue(hotkey.Key, out var commandId) || commandId != hotkey.Value).ToImmutableDictionary();
        var added = Hotkeys.Where(hotkey => !previous.Hotkeys.TryGetValue(hotkey.Key, out var commandId) || commandId != hotkey.Value).ToImmutableDictionary();
        return (removed, added);
    }

    /// <summary>Creates a snapshot excluding the specified combinations while retaining its mode and conflicts.</summary>
    /// <param name="hotkeys">Normalized combinations to exclude, typically failed native registrations.</param>
    /// <returns>A snapshot containing the remaining bindings.</returns>
    public HotkeyRegistration WithoutHotkeys(IEnumerable<HotkeySettings> hotkeys)
    {
        return new HotkeyRegistration(UseLowLevelGlobalHotkey, Hotkeys.RemoveRange(hotkeys), Conflicts);
    }

    /// <summary>Compares effective registrations, ignoring enumeration order and rejected bindings.</summary>
    /// <param name="other">Snapshot to compare against.</param>
    /// <returns>Whether both snapshots request identical registrations; <see langword="false"/> for <see langword="null"/>.</returns>
    public bool Matches(HotkeyRegistration? other)
    {
        return other is not null
               && UseLowLevelGlobalHotkey == other.UseLowLevelGlobalHotkey
               && Hotkeys.Count == other.Hotkeys.Count
               && Hotkeys.All(hotkey => other.Hotkeys.TryGetValue(hotkey.Key, out var commandId) && commandId == hotkey.Value);
    }
}
