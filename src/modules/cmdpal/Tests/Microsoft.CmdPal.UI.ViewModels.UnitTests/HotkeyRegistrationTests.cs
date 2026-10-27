// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class HotkeyRegistrationTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WindowsKeyMode_ChangesOnlyActivationBinding(bool useHook)
    {
        var settings = new SettingsModel { EnableDock = true, UseLowLevelGlobalHotkey = useHook };
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        ImmutableList<TopLevelHotkey> bindings = [new(key, "command")];
        var original = new HotkeyRegistration(settings, bindings);
        var windowsKey = new HotkeyRegistration(settings with { UseWindowsKey = true }, bindings);

        Assert.AreEqual(useHook, windowsKey.UseLowLevelGlobalHotkey);
        Assert.AreEqual(!SettingsModel.IsWindowsKeyHotkeyFeatureEnabled, windowsKey.Hotkeys.ContainsKey(settings.Hotkey!));
        Assert.AreEqual(DockHotkeyIds.FocusDock, windowsKey.Hotkeys[settings.DockFocusHotkey!]);
        Assert.AreEqual("command", windowsKey.Hotkeys[key]);
        var (removed, added) = windowsKey.GetChanges(original);
        Assert.AreEqual(SettingsModel.IsWindowsKeyHotkeyFeatureEnabled ? 1 : 0, removed.Count);
        Assert.IsEmpty(added);

        var (removedOnRestore, addedOnRestore) = original.GetChanges(windowsKey);
        Assert.IsEmpty(removedOnRestore);
        Assert.AreEqual(SettingsModel.IsWindowsKeyHotkeyFeatureEnabled ? 1 : 0, addedOnRestore.Count);
    }

    [TestMethod]
    public void WindowsKeyMode_ReleasesActivationCombinationForCommands()
    {
        var settings = new SettingsModel { UseWindowsKey = true, EnableDock = false };
        var registration = new HotkeyRegistration(settings, [new(settings.Hotkey, "command")]);

        Assert.AreEqual(SettingsModel.IsWindowsKeyHotkeyFeatureEnabled ? "command" : string.Empty, registration.Hotkeys[settings.Hotkey!]);
        Assert.AreEqual(SettingsModel.IsWindowsKeyHotkeyFeatureEnabled ? 0 : 1, registration.Conflicts.Count);
    }

    [TestMethod]
    public void InitialRegistration_IsRequiredEvenWithoutHotkeys()
    {
        var registration = new HotkeyRegistration(new SettingsModel { Hotkey = null, EnableDock = false }, []);

        Assert.IsEmpty(registration.Hotkeys);
        Assert.IsFalse(registration.Matches(null));
        Assert.IsTrue(registration.Matches(new HotkeyRegistration(new SettingsModel { Hotkey = null, EnableDock = false }, [])));
    }

    [TestMethod]
    public void EmptyShortcuts_AreExcluded()
    {
        var key = new HotkeySettings();
        var settings = new SettingsModel { Hotkey = key, EnableDock = true, DockFocusHotkey = key };

        var registration = new HotkeyRegistration(settings, [new(key, "command")]);

        Assert.IsEmpty(registration.Hotkeys);
        Assert.IsTrue(registration.Matches(new HotkeyRegistration(new SettingsModel { Hotkey = null, EnableDock = false }, [])));
    }

    [TestMethod]
    [DataRow(false, false, 124)]
    [DataRow(true, false, 0)]
    [DataRow(false, true, 9)]
    public void NonEmptyShortcuts_AreKeptRegardlessOfPickerValidation(bool ctrl, bool shift, int code)
    {
        var key = new HotkeySettings { Ctrl = ctrl, Shift = shift, Code = code };
        var settings = new SettingsModel { Hotkey = null, EnableDock = false };

        var activation = new HotkeyRegistration(settings with { Hotkey = key }, []);
        var dock = new HotkeyRegistration(settings with { EnableDock = true, DockFocusHotkey = key }, []);
        var command = new HotkeyRegistration(settings, [new(key, "command")]);

        Assert.AreEqual(string.Empty, activation.Hotkeys[key]);
        Assert.AreEqual(DockHotkeyIds.FocusDock, dock.Hotkeys[key]);
        Assert.AreEqual("command", command.Hotkeys[key]);
    }

    [TestMethod]
    public void UnrelatedSettingsAndInactiveBindings_DoNotChangeRegistration()
    {
        var settings = new SettingsModel { EnableDock = false };
        var original = new HotkeyRegistration(settings, []);
        var updated = new HotkeyRegistration(
            settings with
            {
                ShowSystemTrayIcon = !settings.ShowSystemTrayIcon,
                IgnoreShortcutWhenFullscreen = !settings.IgnoreShortcutWhenFullscreen,
                DockFocusHotkey = new HotkeySettings { Ctrl = true, Code = 65 },
                CommandHotkeys = [new(new HotkeySettings { Ctrl = true, Code = 66 }, "unavailable-command")],
            },
            []);

        Assert.IsTrue(updated.Matches(original));
    }

    [TestMethod]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public void HookModeChange_RequiresRegistration(bool previousMode, bool nextMode)
    {
        var settings = new SettingsModel { UseLowLevelGlobalHotkey = previousMode };
        var original = new HotkeyRegistration(settings, []);
        var updated = new HotkeyRegistration(settings with { UseLowLevelGlobalHotkey = nextMode }, []);

        Assert.IsFalse(updated.Matches(original));
        Assert.IsTrue(updated.Matches(new HotkeyRegistration(settings with { UseLowLevelGlobalHotkey = nextMode }, [])));
    }

    [TestMethod]
    [DataRow(true, true, false, false, 65)]
    [DataRow(false, false, false, false, 65)]
    [DataRow(false, true, true, false, 65)]
    [DataRow(false, true, false, true, 65)]
    [DataRow(false, true, false, false, 66)]
    public void ActivationShortcutChange_RequiresRegistration(bool win, bool ctrl, bool alt, bool shift, int code)
    {
        var settings = new SettingsModel { Hotkey = new HotkeySettings { Ctrl = true, Code = 65 } };
        var original = new HotkeyRegistration(settings, []);
        var updated = new HotkeyRegistration(settings with { Hotkey = new HotkeySettings(win, ctrl, alt, shift, code) }, []);

        Assert.IsFalse(updated.Matches(original));
        Assert.IsFalse(new HotkeyRegistration(settings with { Hotkey = null }, []).Matches(original));
    }

    [TestMethod]
    public void DockShortcut_ChangesRegistrationOnlyWhileEnabled()
    {
        var settings = new SettingsModel { EnableDock = false };
        var disabled = new HotkeyRegistration(settings, []);
        var enabled = new HotkeyRegistration(settings with { EnableDock = true }, []);
        var changed = new HotkeyRegistration(
            settings with
            {
                EnableDock = true,
                DockFocusHotkey = new HotkeySettings { Ctrl = true, Code = 65 },
            },
            []);

        Assert.IsFalse(enabled.Matches(disabled));
        Assert.IsFalse(disabled.Matches(enabled));
        Assert.IsFalse(changed.Matches(enabled));
        Assert.AreEqual(DockHotkeyIds.FocusDock, changed.Hotkeys[new HotkeySettings { Ctrl = true, Code = 65 }]);
    }

    [TestMethod]
    public void ActiveCommandBindingChangeOrRemoval_RequiresRegistration()
    {
        var settings = new SettingsModel();
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        var original = new HotkeyRegistration(settings, [new(key, "command")]);

        Assert.IsFalse(new HotkeyRegistration(settings, [new(key with { Code = 66 }, "command")]).Matches(original));
        Assert.IsFalse(new HotkeyRegistration(settings, [new(key, "another-command")]).Matches(original));
        Assert.IsFalse(new HotkeyRegistration(settings, []).Matches(original));
        Assert.IsFalse(original.Matches(new HotkeyRegistration(settings, [])));
    }

    [TestMethod]
    public void ReorderedBindingsAndDisplayMetadata_DoNotChangeRegistration()
    {
        var settings = new SettingsModel { EnableDock = true };
        ImmutableList<TopLevelHotkey> bindings =
        [
            new(new HotkeySettings { Ctrl = true, Code = 65 }, "first"),
            new(new HotkeySettings { Ctrl = true, Code = 66 }, "second"),
        ];
        var original = new HotkeyRegistration(settings, bindings);
        var updated = new HotkeyRegistration(
            settings with
            {
                Hotkey = settings.Hotkey! with { Key = "activation label" },
                DockFocusHotkey = settings.DockFocusHotkey! with { Key = "dock label" },
            },
            bindings.Reverse().Select(binding => binding with { Hotkey = binding.Hotkey! with { Key = "command label" } }).ToImmutableList());

        Assert.IsTrue(updated.Matches(original));
    }

    [TestMethod]
    public void NullCommandBinding_DoesNotChangeRegistration()
    {
        var settings = new SettingsModel();

        Assert.IsTrue(new HotkeyRegistration(settings, [new(null, "command")]).Matches(new HotkeyRegistration(settings, [])));
    }

    [TestMethod]
    public void DuplicateShortcut_PreservesActivationThenDockThenCommandPriority()
    {
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        var settings = new SettingsModel { Hotkey = key, EnableDock = true, DockFocusHotkey = key };
        ImmutableList<TopLevelHotkey> bindings = [new(key, "first"), new(key, "second")];
        var activation = new HotkeyRegistration(settings, bindings);
        var dock = new HotkeyRegistration(settings with { Hotkey = null }, bindings);
        var command = new HotkeyRegistration(settings with { Hotkey = null, EnableDock = false }, bindings);

        Assert.HasCount(1, activation.Hotkeys);
        Assert.AreEqual(string.Empty, activation.Hotkeys[key]);
        Assert.AreEqual(DockHotkeyIds.FocusDock, dock.Hotkeys[key]);
        Assert.AreEqual("first", command.Hotkeys[key]);
        Assert.IsTrue(activation.Matches(new HotkeyRegistration(settings, [])));
        Assert.IsFalse(command.Matches(new HotkeyRegistration(settings with { Hotkey = null, EnableDock = false }, bindings.Reverse())));
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public void DuplicateShortcut_RecordsSkippedCommand(bool activationWins, bool dockWins)
    {
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        var settings = new SettingsModel { Hotkey = activationWins ? key : null, EnableDock = dockWins, DockFocusHotkey = key };
        var winner = activationWins ? string.Empty : dockWins ? DockHotkeyIds.FocusDock : "existing-command";
        ImmutableList<TopLevelHotkey> bindings = activationWins || dockWins ? [] : [new(key, winner)];
        bindings = bindings.Add(new(key with { Key = "Different display text" }, "skipped-command"));
        var registration = new HotkeyRegistration(settings, bindings);

        Assert.AreEqual(winner, registration.Hotkeys[key]);
        Assert.AreEqual(winner, registration.Conflicts[new(key, "skipped-command")]);
        Assert.HasCount(1, registration.Conflicts);
    }

    [TestMethod]
    public void CommandLoadAndRemoval_OnlyChangeThatBinding()
    {
        var settings = new SettingsModel { EnableDock = true };
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        var original = new HotkeyRegistration(settings, []);
        var loaded = new HotkeyRegistration(settings, [new(key, "command")]);

        var load = loaded.GetChanges(original);
        Assert.IsEmpty(load.Removed);
        Assert.HasCount(1, load.Added);
        Assert.AreEqual("command", load.Added[key]);

        var removal = original.GetChanges(loaded);
        Assert.HasCount(1, removal.Removed);
        Assert.IsTrue(removal.Removed.ContainsKey(key));
        Assert.IsEmpty(removal.Added);
        Assert.IsEmpty(loaded.GetChanges(loaded).Added);
        Assert.IsEmpty(loaded.GetChanges(loaded).Removed);
    }

    [TestMethod]
    public void CommandReassignment_RemovesOldTargetAndAddsNewTarget()
    {
        var settings = new SettingsModel();
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        var original = new HotkeyRegistration(settings, [new(key, "original")]);
        var updated = new HotkeyRegistration(settings, [new(key, "replacement")]);

        var changes = updated.GetChanges(original);

        Assert.HasCount(1, changes.Removed);
        Assert.HasCount(1, changes.Added);
        Assert.AreEqual("original", changes.Removed[key]);
        Assert.AreEqual("replacement", changes.Added[key]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void HookModeChange_ReplacesEveryRegistration(bool useHook)
    {
        var settings = new SettingsModel { EnableDock = true, UseLowLevelGlobalHotkey = useHook };
        var original = new HotkeyRegistration(settings, []);
        var updated = new HotkeyRegistration(settings with { UseLowLevelGlobalHotkey = !useHook }, []);

        var changes = updated.GetChanges(original);

        Assert.AreSame(original.Hotkeys, changes.Removed);
        Assert.AreSame(updated.Hotkeys, changes.Added);
        Assert.AreSame(original.Hotkeys, original.GetChanges(null).Added);
        Assert.IsEmpty(original.GetChanges(null).Removed);
    }

    [TestMethod]
    public void FailedRegistration_IsRetriedWithoutRemovingSuccessfulKeys()
    {
        var settings = new SettingsModel { EnableDock = true };
        var desired = new HotkeyRegistration(settings, []);
        var successful = desired.WithoutHotkeys([settings.Hotkey!]);

        var retry = desired.GetChanges(successful);

        Assert.IsFalse(desired.Matches(successful));
        Assert.IsEmpty(retry.Removed);
        Assert.HasCount(1, retry.Added);
        Assert.AreEqual(string.Empty, retry.Added[settings.Hotkey!]);
        Assert.IsTrue(desired.Matches(desired.WithoutHotkeys([])));
    }
}
