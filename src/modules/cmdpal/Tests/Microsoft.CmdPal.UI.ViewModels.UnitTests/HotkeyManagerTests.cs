// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
[System.Diagnostics.CodeAnalysis.SuppressMessage("IDisposable", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed in TestCleanup")]
public sealed partial class HotkeyManagerTests
{
    private const string ProviderId = "test-provider";
    private const string CommandId = "test-command";
    private const string FallbackId = "test-fallback";

    private readonly QueuedTaskScheduler _uiScheduler = new();
    private readonly QueuedTaskScheduler _viewModelScheduler = new();
    private Mock<ISettingsService> _settingsService = null!;
    private ServiceProvider _services = null!;
    private TopLevelCommandManager _commands = null!;
    private HotkeyManager _hotkeys = null!;
    private CommandProviderWrapper _provider = null!;
    private TopLevelViewModel[] _loadedCommands = [];

    [TestInitialize]
    public async Task Initialize()
    {
        var settings = new SettingsModel
        {
            CommandHotkeys =
            [
                new(new HotkeySettings { Ctrl = true, Code = 65 }, CommandId),
                new(new HotkeySettings { Ctrl = true, Code = 66 }, FallbackId),
                new(new HotkeySettings { Ctrl = true, Code = 67 }, "missing-command"),
            ],
        };
        _settingsService = new Mock<ISettingsService>();
        _settingsService.SetupGet(service => service.Settings).Returns(() => settings);
        _settingsService.Setup(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((update, hotReload) =>
            {
                settings = update(settings);
                if (hotReload)
                {
                    _settingsService.Raise(service => service.SettingsChanged += null, _settingsService.Object, settings);
                }
            });
        _services = new ServiceCollection()
            .AddSingleton(TaskScheduler.Default)
            .AddSingleton(_settingsService.Object)
            .AddSingleton<HotkeyManager>(_ => _hotkeys)
            .BuildServiceProvider();
        _provider = new CommandProviderWrapper(new TestCommandProvider(), _viewModelScheduler);
        await _provider.LoadTopLevelCommands(_services);
        _loadedCommands = [.. _provider.TopLevelItems, .. _provider.FallbackItems];
        _commands = new TopLevelCommandManager(_services, []);
        foreach (var command in _loadedCommands)
        {
            _commands.TopLevelCommands.Add(command);
        }

        _hotkeys = new HotkeyManager(_commands, _settingsService.Object, _uiScheduler);
        await new BatchUpdateBarrier().Completion.WaitAsync(TimeSpan.FromSeconds(5));
        _viewModelScheduler.ExecuteAll();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _hotkeys.Dispose();
        _commands.Dispose();
        foreach (var command in _loadedCommands)
        {
            command.Cleanup();
        }

        _viewModelScheduler.ExecuteAll();
        _services.Dispose();
    }

    [TestMethod]
    public void ActiveCommandHotkeys_ExcludesMissingCommandsWithoutDeletingBindings()
    {
        CollectionAssert.AreEqual(new[] { CommandId, FallbackId }, _hotkeys.ActiveCommandHotkeys.Select(hotkey => hotkey.CommandId).ToArray());
        Assert.AreEqual(3, _settingsService.Object.Settings.CommandHotkeys.Count);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void MissingCommandId_DoesNotBlockValidBindings(string? commandId)
    {
        var invalid = new TopLevelHotkey(new HotkeySettings { Ctrl = true, Code = 70 }, commandId!);
        _settingsService.Object.UpdateSettings(settings => settings with { CommandHotkeys = settings.CommandHotkeys.Add(invalid) });
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();

        CollectionAssert.AreEqual(new[] { CommandId, FallbackId }, _hotkeys.ActiveCommandHotkeys.Select(hotkey => hotkey.CommandId).ToArray());
        CollectionAssert.Contains(_settingsService.Object.Settings.CommandHotkeys.ToArray(), invalid);
        using var initial = new HotkeyManager(_commands, _settingsService.Object, _uiScheduler);
        CollectionAssert.AreEqual(_hotkeys.ActiveCommandHotkeys.ToArray(), initial.ActiveCommandHotkeys.ToArray());
    }

    [TestMethod]
    public void ActiveCommandHotkeys_FollowsCommandLoadingRemovalAndReload()
    {
        var saved = _settingsService.Object.Settings.CommandHotkeys;
        var activeCounts = new List<int>();
        _hotkeys.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HotkeyManager.ActiveCommandHotkeys))
            {
                activeCounts.Add(_hotkeys.ActiveCommandHotkeys.Count);
            }
        };

        _commands.TopLevelCommands.Clear();
        _uiScheduler.ExecuteAll();
        _commands.TopLevelCommands.Add(_loadedCommands[0]);
        _uiScheduler.ExecuteAll();
        _commands.TopLevelCommands.Remove(_loadedCommands[0]);
        _uiScheduler.ExecuteAll();
        _commands.TopLevelCommands.Add(_loadedCommands[0]);
        _uiScheduler.ExecuteAll();
        _commands.TopLevelCommands.Add(_loadedCommands[1]);
        _uiScheduler.ExecuteAll();

        int[] expectedCounts = [0, 1, 0, 1, 2];
        CollectionAssert.AreEqual(expectedCounts, activeCounts);
        Assert.AreSame(saved, _settingsService.Object.Settings.CommandHotkeys);
    }

    [TestMethod]
    [DataRow(false, true, 0)]
    [DataRow(false, false, 0)]
    [DataRow(true, false, 1)]
    public void ActiveCommandHotkeys_UsesCurrentEnabledSettings(bool providerEnabled, bool fallbackEnabled, int expectedCount)
    {
        var saved = _settingsService.Object.Settings.CommandHotkeys;
        _settingsService.Object.UpdateSettings(settings => settings with
        {
            ProviderSettings = settings.ProviderSettings.SetItem(ProviderId, settings.ProviderSettings[ProviderId] with
            {
                IsEnabled = providerEnabled,
                FallbackCommands = settings.ProviderSettings[ProviderId].FallbackCommands.SetItem(FallbackId, new FallbackSettings { IsEnabled = fallbackEnabled }),
            }),
        });

        _uiScheduler.ExecuteAll();
        Assert.IsTrue(_loadedCommands.All(command => command.IsEnabled), "Loaded commands still have their original settings snapshot.");
        Assert.AreEqual(expectedCount, _hotkeys.ActiveCommandHotkeys.Count);
        if (expectedCount == 1)
        {
            Assert.AreEqual(CommandId, _hotkeys.ActiveCommandHotkeys[0].CommandId);
        }

        _settingsService.Object.UpdateSettings(settings => settings with
        {
            ProviderSettings = settings.ProviderSettings.SetItem(ProviderId, new ProviderSettings()),
        });

        _uiScheduler.ExecuteAll();
        Assert.AreEqual(2, _hotkeys.ActiveCommandHotkeys.Count);
        Assert.AreSame(saved, _settingsService.Object.Settings.CommandHotkeys);
    }

    [TestMethod]
    public void ProviderWithoutSettings_RemainsEnabledForCommandsAndHotkeys()
    {
        _settingsService.Object.UpdateSettings(settings => settings with
        {
            ProviderSettings = settings.ProviderSettings.Remove(ProviderId),
        });
        _uiScheduler.ExecuteAll();

        Assert.IsTrue(_commands.IsProviderEnabled(ProviderId));
        CollectionAssert.AreEqual(new[] { CommandId, FallbackId }, _hotkeys.ActiveCommandHotkeys.Select(hotkey => hotkey.CommandId).ToArray());
    }

    [TestMethod]
    public void ActiveCommandHotkeys_UpdatesWhenBindingChanges()
    {
        var replacement = new HotkeySettings { Alt = true, Code = 68 };

        _hotkeys.UpdateHotkey(CommandId, replacement);
        _uiScheduler.ExecuteAll();

        Assert.AreEqual(replacement, _hotkeys.ActiveCommandHotkeys.Single(hotkey => hotkey.CommandId == CommandId).Hotkey);

        _hotkeys.UpdateHotkey(CommandId, null);
        _uiScheduler.ExecuteAll();

        Assert.AreEqual(FallbackId, _hotkeys.ActiveCommandHotkeys.Single().CommandId);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CommandViewModel_DisablingClearsShortcutAndPersistedBinding(bool useEmptyHotkey)
    {
        var remaining = _settingsService.Object.Settings.CommandHotkeys.RemoveAll(hotkey => hotkey.CommandId == CommandId);
        var command = _loadedCommands.Single(item => item.Id == CommandId);
        var assigned = new HotkeySettings { Ctrl = true, Code = 65 };
        command.Hotkey = assigned;
        Assert.AreEqual(assigned, command.Hotkey);

        command.Hotkey = useEmptyHotkey ? new HotkeySettings() : null;
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();

        Assert.IsNull(command.Hotkey);
        Assert.IsEmpty(command.Tags);
        CollectionAssert.AreEqual(remaining.ToArray(), _settingsService.Object.Settings.CommandHotkeys.ToArray());
        Assert.AreEqual(FallbackId, _hotkeys.ActiveCommandHotkeys.Single().CommandId);

        _settingsService.Invocations.Clear();
        command.Hotkey = useEmptyHotkey ? new HotkeySettings() : null;
        _settingsService.Verify(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Never);
        _settingsService.Verify(service => service.Save(It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("A")]
    public void Reassignment_RefreshesLosingCommandsShortcutAndTags(string previousLabel)
    {
        var first = _loadedCommands.Single(command => command.Id == CommandId);
        var second = _loadedCommands.Single(command => command.Id == FallbackId);
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        _hotkeys.UpdateHotkey(CommandId, key with { Key = previousLabel });
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();
        Assert.AreEqual(key.ToString(), first.Tags.Single().Text);
        var notifications = 0;
        first.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TopLevelViewModel.Hotkey))
            {
                Assert.AreSame(_viewModelScheduler, TaskScheduler.Current);
                notifications++;
            }
        };

        second.Hotkey = key;
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();

        Assert.IsNull(first.Hotkey);
        Assert.IsEmpty(first.Tags);
        Assert.AreEqual(key, second.Hotkey);
        Assert.AreEqual(key.ToString(), second.Tags.Single().Text);
        Assert.AreEqual(1, notifications);
        _settingsService.Object.UpdateSettings(settings => settings with { ShowSystemTrayIcon = !settings.ShowSystemTrayIcon });
        _viewModelScheduler.ExecuteAll();
        Assert.AreEqual(1, notifications);
    }

    [TestMethod]
    public async Task BackgroundSettingsChange_RefreshesShortcutOnUiScheduler()
    {
        var command = _loadedCommands.Single(item => item.Id == CommandId);
        var original = new HotkeySettings { Ctrl = true, Code = 65 };
        command.Hotkey = original;
        _viewModelScheduler.ExecuteAll();
        var notifications = 0;
        command.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TopLevelViewModel.Hotkey))
            {
                Assert.AreSame(_viewModelScheduler, TaskScheduler.Current);
                notifications++;
            }
        };

        await Task.Run(() => _settingsService.Object.UpdateSettings(settings => settings with { CommandHotkeys = [] }));
        Assert.AreEqual(original, command.Hotkey);
        Assert.AreEqual(0, _viewModelScheduler.PendingCount);
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();

        Assert.IsNull(command.Hotkey);
        Assert.IsEmpty(command.Tags);
        Assert.AreEqual(1, notifications);
    }

    [TestMethod]
    public void CommandCleanup_CancelsPendingShortcutRefresh()
    {
        var command = _loadedCommands.Single(item => item.Id == CommandId);
        command.Hotkey = new HotkeySettings { Ctrl = true, Code = 65 };
        _viewModelScheduler.ExecuteAll();
        var original = command.Hotkey;
        var notifications = 0;
        command.PropertyChanged += (_, _) => notifications++;
        command.PropChanged += (_, _) => notifications++;

        _settingsService.Object.UpdateSettings(settings => settings with { CommandHotkeys = [] });
        _uiScheduler.ExecuteAll();
        command.Cleanup();
        _viewModelScheduler.ExecuteAll();
        _settingsService.Object.UpdateSettings(settings => settings with { ShowSystemTrayIcon = !settings.ShowSystemTrayIcon });
        _viewModelScheduler.ExecuteAll();

        Assert.AreEqual(original, command.Hotkey);
        Assert.AreEqual(0, notifications);
    }

    [TestMethod]
    public async Task ProviderReload_DoesNotSubscribeCommandViewModelsToSettings()
    {
        _settingsService.Object.UpdateSettings(settings => settings.TryPinCommand(ProviderId, CommandId) with
        {
            ProviderSettings = settings.ProviderSettings.SetItem(ProviderId, settings.ProviderSettings[ProviderId] with
            {
                FallbackCommands = settings.ProviderSettings[ProviderId].FallbackCommands.SetItem(FallbackId, new FallbackSettings { IsEnabled = false }),
            }),
        });
        var provider = new CommandProviderWrapper(new TestCommandProvider(), _viewModelScheduler);
        try
        {
            for (var i = 0; i < 3; i++)
            {
                await provider.LoadTopLevelCommands(_services);
                _viewModelScheduler.ExecuteAll();
                Assert.HasCount(1, provider.TopLevelItems);
                Assert.IsFalse(provider.FallbackItems.Single().IsEnabled);
            }

            _settingsService.VerifyAdd(service => service.SettingsChanged += It.IsAny<TypedEventHandler<ISettingsService, SettingsModel>>(), Times.Once);
        }
        finally
        {
            foreach (var command in provider.TopLevelItems.Concat(provider.FallbackItems))
            {
                command.Cleanup();
            }
        }
    }

    [TestMethod]
    public void ShortcutChange_QueuesOnlyTheAffectedCommand()
    {
        var replacement = new HotkeySettings { Alt = true, Code = 68 };
        _hotkeys.UpdateHotkey(CommandId, replacement);

        Assert.AreEqual(1, _uiScheduler.PendingCount);
        Assert.AreEqual(0, _viewModelScheduler.PendingCount);
        _uiScheduler.ExecuteAll();
        Assert.AreEqual(1, _viewModelScheduler.PendingCount);
        _viewModelScheduler.ExecuteAll();
        Assert.AreEqual(replacement, _loadedCommands.Single(command => command.Id == CommandId).Hotkey);
        Assert.AreEqual(66, _loadedCommands.Single(command => command.Id == FallbackId).Hotkey!.Code);
    }

    [TestMethod]
    public void RemovedCommand_IsNotRefreshedWithoutCleanup()
    {
        var command = _loadedCommands.Single(item => item.Id == CommandId);
        var original = command.Hotkey;
        _commands.TopLevelCommands.Remove(command);
        _hotkeys.UpdateHotkey(CommandId, null);
        _uiScheduler.ExecuteAll();

        Assert.AreEqual(0, _viewModelScheduler.PendingCount);
        Assert.AreEqual(original, command.Hotkey);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShortcutNotification_DoesNotWriteSettingsBack(bool clearShortcut)
    {
        _settingsService.Invocations.Clear();
        var command = _loadedCommands.Single(item => item.Id == CommandId);
        using var picker = new ShortcutPickerBinding(command);
        var notifications = 0;
        command.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TopLevelViewModel.Hotkey))
            {
                notifications++;
            }
        };

        _hotkeys.UpdateHotkey(CommandId, clearShortcut ? null : new HotkeySettings { Alt = true, Code = 68 });
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();

        Assert.AreEqual(1, notifications);
        Assert.AreEqual(command.Hotkey, picker.Hotkey);
        _settingsService.Verify(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Once);
        _settingsService.Verify(service => service.Save(It.IsAny<bool>()), Times.Never);
        Assert.AreEqual(0, _uiScheduler.PendingCount);
        Assert.AreEqual(0, _viewModelScheduler.PendingCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CommandViewModel_UpdatesShortcutWithOneSettingsUpdate(bool clearShortcut)
    {
        _settingsService.Invocations.Clear();
        var command = _loadedCommands.Single(item => item.Id == CommandId);
        using var picker = new ShortcutPickerBinding(command);
        var replacement = clearShortcut ? null : new HotkeySettings { Alt = true, Code = 68 };
        var notifications = 0;
        command.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TopLevelViewModel.Hotkey))
            {
                notifications++;
            }
        };

        picker.SetHotkey(replacement ?? new HotkeySettings());
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();

        Assert.AreEqual(replacement, command.Hotkey);
        Assert.AreEqual(replacement, picker.Hotkey);
        Assert.AreEqual(1, notifications);
        CollectionAssert.AreEqual(replacement is null ? Array.Empty<string>() : new[] { replacement.ToString() }, command.Tags.Select(tag => tag.Text).ToArray());
        _settingsService.Verify(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Once);
        _settingsService.Verify(service => service.Save(It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    [DataRow("", "A", false)]
    [DataRow("A", "", false)]
    [DataRow("", "", false)]
    [DataRow("", "A", true)]
    [DataRow("A", "", true)]
    [DataRow("", "", true)]
    public void RealizingDuplicateShortcutPickers_DoesNotChangeBindings(string firstLabel, string secondLabel, bool reverseOrder)
    {
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        _settingsService.Object.UpdateSettings(settings => settings with
        {
            CommandHotkeys =
            [
                new(key with { Key = firstLabel }, CommandId),
                new(key with { Key = secondLabel }, FallbackId),
            ],
        });
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();
        var first = _loadedCommands.Single(command => command.Id == CommandId);
        var second = _loadedCommands.Single(command => command.Id == FallbackId);
        Assert.AreEqual(CommandId, new HotkeyRegistration(_settingsService.Object.Settings, _hotkeys.ActiveCommandHotkeys).Hotkeys[key]);
        var saved = _settingsService.Object.Settings;
        _settingsService.Invocations.Clear();

        using var firstPicker = new ShortcutPickerBinding(reverseOrder ? second : first);
        using var secondPicker = new ShortcutPickerBinding(reverseOrder ? first : second);
        second.Hotkey = second.Hotkey! with { };
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();

        Assert.AreSame(saved, _settingsService.Object.Settings);
        Assert.AreEqual(key.ToString(), first.Tags.Single().Text);
        Assert.AreEqual(key.ToString(), second.Tags.Single().Text);
        Assert.AreEqual(CommandId, new HotkeyRegistration(saved, _hotkeys.ActiveCommandHotkeys).Hotkeys[key]);
        _settingsService.Verify(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Never);
        _settingsService.Verify(service => service.Save(It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EditingDuplicateShortcut_ClaimsBindingOnlyWhenPickerValueChanges(bool clearFirst)
    {
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        _settingsService.Object.UpdateSettings(settings => settings with
        {
            CommandHotkeys = [new(key, CommandId), new(key with { Key = "A" }, FallbackId)],
        });
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();
        var command = _loadedCommands.Single(item => item.Id == FallbackId);
        using var picker = new ShortcutPickerBinding(command);
        _settingsService.Invocations.Clear();

        if (clearFirst)
        {
            picker.SetHotkey(new HotkeySettings());
        }

        picker.SetHotkey(key);
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();

        Assert.AreEqual(FallbackId, _settingsService.Object.Settings.CommandHotkeys.Single().CommandId);
        Assert.AreEqual(key, picker.Hotkey);
        Assert.AreEqual(FallbackId, new HotkeyRegistration(_settingsService.Object.Settings, _hotkeys.ActiveCommandHotkeys).Hotkeys[key]);
        _settingsService.Verify(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Exactly(clearFirst ? 2 : 1));
        _settingsService.Verify(service => service.Save(It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("A")]
    public void LegacyEmptyShortcut_IsUnassignedWithoutRewritingSettings(string keyLabel)
    {
        var empty = new HotkeySettings { Key = keyLabel };
        _settingsService.Object.UpdateSettings(settings => settings with { CommandHotkeys = [new(empty, CommandId)] });
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();
        var saved = _settingsService.Object.Settings;
        _settingsService.Invocations.Clear();

        var command = _loadedCommands.Single(command => command.Id == CommandId);
        using var picker = new ShortcutPickerBinding(command);

        Assert.AreSame(saved, _settingsService.Object.Settings);
        Assert.AreSame(empty, saved.CommandHotkeys.Single().Hotkey);
        Assert.IsNull(saved.GetCommandHotkey(CommandId));
        Assert.IsNull(command.Hotkey);
        Assert.IsNull(picker.Hotkey);
        Assert.IsEmpty(command.Tags);
        _settingsService.Verify(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Never);
        _settingsService.Verify(service => service.Save(It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void IndexedDisplayLookup_UsesFirstBindingIncludingNull(bool firstIsNull)
    {
        var first = firstIsNull ? null : new HotkeySettings { Alt = true, Code = 68, Key = "D" };
        _settingsService.Object.UpdateSettings(settings => settings with
        {
            CommandHotkeys =
            [
                new(first, CommandId),
                new(new HotkeySettings { Ctrl = true, Code = 69 }, CommandId),
            ],
        });
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();

        var command = _loadedCommands.Single(item => item.Id == CommandId);
        Assert.AreSame(first, _settingsService.Object.Settings.GetCommandHotkey(CommandId));
        Assert.AreSame(first, command.Hotkey);
        CollectionAssert.AreEqual(first is null ? Array.Empty<string>() : new[] { first.ToString() }, command.Tags.Select(tag => tag.Text).ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DetachedCommand_DisplayEchoRefreshesCacheWithoutSaving(bool clearBinding)
    {
        var command = _loadedCommands.Single(item => item.Id == CommandId);
        var cached = command.Hotkey;
        var replacement = clearBinding ? null : new HotkeySettings { Alt = true, Code = 68 };
        _settingsService.Object.UpdateSettings(settings => settings with
        {
            ProviderSettings = settings.ProviderSettings.SetItem(ProviderId, settings.ProviderSettings[ProviderId] with { IsEnabled = false }),
        });
        _commands.TopLevelCommands.Remove(command);
        _hotkeys.UpdateHotkey(CommandId, replacement);
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();
        Assert.AreEqual(cached, command.Hotkey);
        _settingsService.Invocations.Clear();

        using var picker = new ShortcutPickerBinding(command);
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();

        Assert.AreEqual(replacement, _settingsService.Object.Settings.GetCommandHotkey(CommandId));
        Assert.AreEqual(replacement, command.Hotkey);
        Assert.AreEqual(replacement, picker.Hotkey);
        CollectionAssert.AreEqual(replacement is null ? Array.Empty<string>() : new[] { replacement.ToString() }, command.Tags.Select(tag => tag.Text).ToArray());
        _settingsService.Verify(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Never);
        _settingsService.Verify(service => service.Save(It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public async Task DisabledProviderPickerRealization_DoesNotStealAnotherProvidersShortcut()
    {
        const string otherCommandId = "other-command";
        var provider = new CommandProviderWrapper(new TestCommandProvider("other-provider", otherCommandId), _viewModelScheduler);
        await provider.LoadTopLevelCommands(_services);
        var otherCommand = provider.TopLevelItems.Single();
        _commands.TopLevelCommands.Add(otherCommand);
        try
        {
            await new BatchUpdateBarrier().Completion.WaitAsync(TimeSpan.FromSeconds(5));
            _uiScheduler.ExecuteAll();
            _viewModelScheduler.ExecuteAll();
            var disabledCommand = _loadedCommands.Single(command => command.Id == CommandId);
            var cached = disabledCommand.Hotkey;
            _settingsService.Object.UpdateSettings(settings => settings with
            {
                ProviderSettings = settings.ProviderSettings.SetItem(ProviderId, settings.ProviderSettings[ProviderId] with { IsEnabled = false }),
            });
            foreach (var command in _loadedCommands)
            {
                _commands.TopLevelCommands.Remove(command);
                command.Cleanup();
            }

            otherCommand.Hotkey = cached;
            _uiScheduler.ExecuteAll();
            _viewModelScheduler.ExecuteAll();
            Assert.AreEqual(cached, disabledCommand.Hotkey);
            var saved = _settingsService.Object.Settings;
            _settingsService.Invocations.Clear();

            using var picker = new ShortcutPickerBinding(disabledCommand);
            _uiScheduler.ExecuteAll();
            _viewModelScheduler.ExecuteAll();

            Assert.IsNull(picker.Hotkey);
            Assert.IsNull(disabledCommand.Hotkey);
            Assert.AreSame(saved, _settingsService.Object.Settings);
            Assert.AreEqual(cached, saved.GetCommandHotkey(otherCommandId));
            Assert.AreEqual(otherCommandId, _hotkeys.ActiveCommandHotkeys.Single().CommandId);
            Assert.AreEqual(otherCommandId, new HotkeyRegistration(saved, _hotkeys.ActiveCommandHotkeys).Hotkeys[cached!]);
            _settingsService.Verify(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Never);
            _settingsService.Verify(service => service.Save(It.IsAny<bool>()), Times.Never);
        }
        finally
        {
            _commands.TopLevelCommands.Remove(otherCommand);
            foreach (var command in provider.TopLevelItems.Concat(provider.FallbackItems))
            {
                command.Cleanup();
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DuplicateCommandViewModels_UseCurrentBindings(bool realizeBeforeRefresh)
    {
        var provider = new CommandProviderWrapper(new TestCommandProvider(), _viewModelScheduler);
        await provider.LoadTopLevelCommands(_services);
        var duplicate = provider.TopLevelItems.Single();
        _commands.TopLevelCommands.Add(duplicate);
        try
        {
            await new BatchUpdateBarrier().Completion.WaitAsync(TimeSpan.FromSeconds(5));
            _uiScheduler.ExecuteAll();
            _viewModelScheduler.ExecuteAll();
            var replacement = new HotkeySettings { Alt = true, Code = 68 };
            _hotkeys.UpdateHotkey(CommandId, replacement);
            _settingsService.Invocations.Clear();
            if (realizeBeforeRefresh)
            {
                using var picker = new ShortcutPickerBinding(duplicate);
                Assert.AreEqual(replacement, picker.Hotkey);
            }

            _uiScheduler.ExecuteAll();
            _viewModelScheduler.ExecuteAll();

            Assert.AreEqual(replacement, _settingsService.Object.Settings.GetCommandHotkey(CommandId));
            foreach (var command in _commands.TopLevelCommands.Where(command => command.Id == CommandId))
            {
                Assert.AreEqual(replacement, command.Hotkey);
                Assert.AreEqual(replacement.ToString(), command.Tags.Single().Text);
            }

            _settingsService.Verify(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Never);
            _settingsService.Verify(service => service.Save(It.IsAny<bool>()), Times.Never);
        }
        finally
        {
            _commands.TopLevelCommands.Remove(duplicate);
            foreach (var command in provider.TopLevelItems.Concat(provider.FallbackItems))
            {
                command.Cleanup();
            }
        }
    }

    [TestMethod]
    [DataRow("A", "")]
    [DataRow("", "A")]
    public void UpdateHotkey_ReassignsMatchingKeysIgnoringDisplayText(string previousLabel, string newLabel)
    {
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        _hotkeys.UpdateHotkey(CommandId, key with { Key = previousLabel });

        var reassigned = key with { Key = newLabel };
        _hotkeys.UpdateHotkey(FallbackId, reassigned);
        _uiScheduler.ExecuteAll();

        var settings = _settingsService.Object.Settings;
        Assert.IsFalse(settings.CommandHotkeys.Any(hotkey => hotkey.CommandId == CommandId));
        Assert.AreEqual(reassigned, settings.CommandHotkeys.Single(hotkey => hotkey.CommandId == FallbackId).Hotkey);
        Assert.IsTrue(settings.CommandHotkeys.Any(hotkey => hotkey.CommandId == "missing-command"));
        var registration = new HotkeyRegistration(settings, _hotkeys.ActiveCommandHotkeys);
        Assert.AreEqual(FallbackId, registration.Hotkeys[key]);
    }

    [TestMethod]
    [DataRow(true, true, false, false, 65)]
    [DataRow(false, false, false, false, 65)]
    [DataRow(false, true, true, false, 65)]
    [DataRow(false, true, false, true, 65)]
    [DataRow(false, true, false, false, 66)]
    public void UpdateHotkey_PreservesDifferentKeyCombinations(bool win, bool ctrl, bool alt, bool shift, int code)
    {
        var original = _settingsService.Object.Settings.CommandHotkeys.Single(hotkey => hotkey.CommandId == CommandId);

        _hotkeys.UpdateHotkey(FallbackId, new HotkeySettings(win, ctrl, alt, shift, code));
        _uiScheduler.ExecuteAll();

        Assert.AreEqual(original, _settingsService.Object.Settings.CommandHotkeys.Single(hotkey => hotkey.CommandId == CommandId));
        Assert.AreEqual(original, _hotkeys.ActiveCommandHotkeys.Single(hotkey => hotkey.CommandId == CommandId));
    }

    [TestMethod]
    public void ActiveCommandHotkeys_DoesNotNotifyForUnchangedBindings()
    {
        var notifications = 0;
        _hotkeys.PropertyChanged += (_, _) => notifications++;

        _settingsService.Object.UpdateSettings(settings => settings with { ShowSystemTrayIcon = !settings.ShowSystemTrayIcon });
        _commands.TopLevelCommands.Move(0, 1);
        _uiScheduler.ExecuteAll();

        Assert.AreEqual(0, notifications);
    }

    [TestMethod]
    public void UnrelatedSettingsChanges_DoNotQueueRefresh()
    {
        _settingsService.Object.UpdateSettings(settings => settings with
        {
            ShowSystemTrayIcon = !settings.ShowSystemTrayIcon,
            Hotkey = new HotkeySettings { Ctrl = true, Code = 68 },
        });

        Assert.AreEqual(0, _uiScheduler.PendingCount);
        Assert.AreEqual(0, _viewModelScheduler.PendingCount);

        _hotkeys.UpdateHotkey(CommandId, new HotkeySettings { Alt = true, Code = 69 });
        Assert.AreEqual(1, _uiScheduler.PendingCount);
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();

        _settingsService.Object.UpdateSettings(settings => settings with { ShowSystemTrayIcon = !settings.ShowSystemTrayIcon });
        Assert.AreEqual(0, _uiScheduler.PendingCount);
        Assert.AreEqual(0, _viewModelScheduler.PendingCount);
    }

    [TestMethod]
    public void SettingsRevertedDuringPublication_AreRefreshedAgain()
    {
        var original = _settingsService.Object.Settings;
        var reverted = false;
        _hotkeys.PropertyChanged += (_, _) =>
        {
            if (!reverted)
            {
                reverted = true;
                _settingsService.Object.UpdateSettings(_ => original);
            }
        };

        _hotkeys.UpdateHotkey(CommandId, new HotkeySettings { Alt = true, Code = 69 });
        _uiScheduler.ExecuteAll();

        Assert.IsTrue(reverted);
        Assert.AreEqual(original.CommandHotkeys[0], _hotkeys.ActiveCommandHotkeys.Single(hotkey => hotkey.CommandId == CommandId));
        Assert.AreEqual(0, _uiScheduler.PendingCount);
    }

    [TestMethod]
    public void StaleSettingsNotification_DoesNotQueueOrHideLaterChanges()
    {
        var original = _settingsService.Object.Settings;
        _hotkeys.UpdateHotkey(CommandId, new HotkeySettings { Alt = true, Code = 69 });
        _uiScheduler.ExecuteAll();

        _settingsService.Raise(service => service.SettingsChanged += null, _settingsService.Object, original);
        Assert.AreEqual(0, _uiScheduler.PendingCount);

        _settingsService.Object.UpdateSettings(_ => original);
        Assert.AreEqual(1, _uiScheduler.PendingCount);
        _uiScheduler.ExecuteAll();
        Assert.AreEqual(original.CommandHotkeys[0], _hotkeys.ActiveCommandHotkeys.Single(hotkey => hotkey.CommandId == CommandId));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [DoNotParallelize]
    public void NotificationFailure_IsLoggedAndKeepsNewHotkeys(bool removeAllBindings)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var listener = new TextWriterTraceListener(output);
        PropertyChangedEventHandler subscriber = (_, _) => throw new InvalidOperationException("Hotkey refresh test failure");
        Trace.Listeners.Add(listener);
        _hotkeys.PropertyChanged += subscriber;
        try
        {
            _settingsService.Object.UpdateSettings(settings => settings with
            {
                CommandHotkeys = removeAllBindings ? [] : settings.CommandHotkeys.RemoveAll(hotkey => hotkey.CommandId == CommandId),
            });
            _uiScheduler.ExecuteAll();
            listener.Flush();

            StringAssert.Contains(output.ToString(), "Failed to refresh active command hotkeys");
            StringAssert.Contains(output.ToString(), "Hotkey refresh test failure");
            Assert.AreEqual(removeAllBindings ? 0 : 1, _hotkeys.ActiveCommandHotkeys.Count);
        }
        finally
        {
            _hotkeys.PropertyChanged -= subscriber;
            Trace.Listeners.Remove(listener);
        }
    }

    [TestMethod]
    public void SettingsReadFailure_RetriesSameSettings()
    {
        _settingsService.Object.UpdateSettings(settings => settings with { CommandHotkeys = [] });
        var currentSettings = _settingsService.Object.Settings;
        _settingsService.SetupGet(service => service.Settings).Throws(new InvalidOperationException("Settings temporarily unavailable"));

        _uiScheduler.ExecuteAll();
        Assert.HasCount(2, _hotkeys.ActiveCommandHotkeys);

        _settingsService.SetupGet(service => service.Settings).Returns(currentSettings);
        _settingsService.Raise(service => service.SettingsChanged += null, _settingsService.Object, currentSettings);
        Assert.AreEqual(1, _uiScheduler.PendingCount);
        _uiScheduler.ExecuteAll();

        Assert.IsEmpty(_hotkeys.ActiveCommandHotkeys);
    }

    [TestMethod]
    [DoNotParallelize]
    public void DisplayRefreshFailure_DoesNotBlockRegistrationAndRetriesBatch()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var listener = new TextWriterTraceListener(output);
        var notifications = 0;
        _hotkeys.PropertyChanged += (_, _) =>
        {
            Assert.IsTrue(_viewModelScheduler.RejectNextTask, "Publish registrations before attempting display refreshes.");
            notifications++;
        };
        Trace.Listeners.Add(listener);
        try
        {
            _settingsService.Object.UpdateSettings(settings => settings with { CommandHotkeys = [] });
            _viewModelScheduler.RejectNextTask = true;
            _uiScheduler.ExecuteAll();
            _viewModelScheduler.ExecuteAll();
            listener.Flush();

            Assert.IsEmpty(_hotkeys.ActiveCommandHotkeys);
            Assert.AreEqual(1, notifications);
            Assert.IsTrue(_loadedCommands.All(command => command.Hotkey is not null));
            StringAssert.Contains(output.ToString(), "Failed to refresh active command hotkeys");

            _settingsService.Raise(service => service.SettingsChanged += null, _settingsService.Object, _settingsService.Object.Settings);
            _uiScheduler.ExecuteAll();
            _viewModelScheduler.ExecuteAll();

            Assert.AreEqual(1, notifications);
            Assert.IsTrue(_loadedCommands.All(command => command.Hotkey is null && command.Tags.Count == 0));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void ConstructorDisplayRefreshFailure_IsLoggedAndCanRetry()
    {
        _hotkeys.Dispose();
        _settingsService.Object.UpdateSettings(settings => settings with { CommandHotkeys = [] }, hotReload: false);
        _viewModelScheduler.RejectNextTask = true;
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var listener = new TextWriterTraceListener(output);
        Trace.Listeners.Add(listener);
        try
        {
            _hotkeys = new HotkeyManager(_commands, _settingsService.Object, _uiScheduler);
            listener.Flush();

            StringAssert.Contains(output.ToString(), "Failed to refresh active command hotkeys");
            Assert.IsEmpty(_hotkeys.ActiveCommandHotkeys);
            Assert.IsTrue(_loadedCommands.All(command => command.Hotkey is not null));

            _settingsService.Raise(service => service.SettingsChanged += null, _settingsService.Object, _settingsService.Object.Settings);
            _uiScheduler.ExecuteAll();
            _viewModelScheduler.ExecuteAll();

            Assert.IsTrue(_loadedCommands.All(command => command.Hotkey is null && command.Tags.Count == 0));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [TestMethod]
    public void CommandHotkeyChange_CoalescesSettingsAndCollectionNotifications()
    {
        var original = new HotkeyRegistration(_settingsService.Object.Settings, _hotkeys.ActiveCommandHotkeys);
        var notifications = new List<HotkeyRegistration>();
        _hotkeys.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HotkeyManager.ActiveCommandHotkeys))
            {
                notifications.Add(new HotkeyRegistration(_settingsService.Object.Settings, _hotkeys.ActiveCommandHotkeys));
            }
        };

        _hotkeys.UpdateHotkey(CommandId, new HotkeySettings { Alt = true, Code = 68 });
        _commands.TopLevelCommands.Move(0, 1);
        _hotkeys.UpdateHotkey(CommandId, new HotkeySettings { Alt = true, Code = 69 });

        Assert.AreEqual(1, _uiScheduler.PendingCount);
        Assert.IsEmpty(notifications);
        _uiScheduler.ExecuteAll();
        Assert.HasCount(1, notifications);
        Assert.IsFalse(notifications[0].Matches(original));
        Assert.AreEqual(69, _hotkeys.ActiveCommandHotkeys.Single(hotkey => hotkey.CommandId == CommandId).Hotkey!.Code);
    }

    [TestMethod]
    public void ActiveCommandHotkeys_PublishesOnUiSchedulerOutsideCollectionLock()
    {
        var notifications = 0;
        _hotkeys.PropertyChanged += (_, _) =>
        {
            Assert.IsFalse(Monitor.IsEntered(_commands.TopLevelCommands));
            Assert.AreSame(_uiScheduler, TaskScheduler.Current);
            notifications++;
        };

        lock (_commands.TopLevelCommands)
        {
            _commands.TopLevelCommands.Remove(_loadedCommands[0]);
            Assert.AreEqual(0, notifications);
        }

        _uiScheduler.ExecuteAll();
        Assert.AreEqual(1, notifications);
    }

    [TestMethod]
    public void RemovingAllBindings_ClearsLoadedCommandShortcutsAndTags()
    {
        _settingsService.Object.UpdateSettings(settings => settings with { CommandHotkeys = [] });
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();

        Assert.IsEmpty(_hotkeys.ActiveCommandHotkeys);
        foreach (var command in _loadedCommands)
        {
            Assert.IsNull(command.Hotkey);
            Assert.IsEmpty(command.Tags);
        }
    }

    [TestMethod]
    public void RefreshBeforeSettingsNotification_ClearsLastShortcutDisplay()
    {
        _commands.TopLevelCommands.Move(0, 1);

        // Model the interval between publishing settings and raising SettingsChanged after saving them.
        _settingsService.Object.UpdateSettings(settings => settings with { CommandHotkeys = [] }, hotReload: false);

        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();

        Assert.IsEmpty(_hotkeys.ActiveCommandHotkeys);
        Assert.IsTrue(_loadedCommands.All(command => command.Hotkey is null && command.Tags.Count == 0));
        _settingsService.Raise(service => service.SettingsChanged += null, _settingsService.Object, _settingsService.Object.Settings);
        Assert.AreEqual(0, _uiScheduler.PendingCount);
        Assert.AreEqual(0, _viewModelScheduler.PendingCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CommandAddedAfterLastBindingRemoved_ClearsStaleDisplay(bool addDuringRefresh)
    {
        var delayed = _loadedCommands.Single(command => command.Id == CommandId);
        _commands.TopLevelCommands.Remove(delayed);
        _uiScheduler.ExecuteAll();
        var added = false;
        if (addDuringRefresh)
        {
            _hotkeys.PropertyChanged += (_, _) =>
            {
                if (!added && _hotkeys.ActiveCommandHotkeys.Count == 0)
                {
                    added = true;
                    _commands.TopLevelCommands.Add(delayed);
                }
            };
        }

        _settingsService.Object.UpdateSettings(settings => settings with { CommandHotkeys = [] });
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();
        if (!addDuringRefresh)
        {
            Assert.IsNotNull(delayed.Hotkey);
            _commands.TopLevelCommands.Add(delayed);
            _uiScheduler.ExecuteAll();
            _viewModelScheduler.ExecuteAll();
        }

        Assert.IsEmpty(_hotkeys.ActiveCommandHotkeys);
        Assert.IsNull(delayed.Hotkey);
        Assert.IsEmpty(delayed.Tags);
        Assert.AreEqual(0, _uiScheduler.PendingCount);
        Assert.AreEqual(0, _viewModelScheduler.PendingCount);
    }

    [TestMethod]
    public void CoalescedChangesEndingWithoutBindings_StillClearCommandDisplay()
    {
        _settingsService.Object.UpdateSettings(settings => settings with { CommandHotkeys = [] });
        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();
        var command = _loadedCommands.Single(item => item.Id == CommandId);
        command.Hotkey = new HotkeySettings { Alt = true, Code = 68 };
        _settingsService.Object.UpdateSettings(settings => settings with { CommandHotkeys = [] });

        _uiScheduler.ExecuteAll();
        _viewModelScheduler.ExecuteAll();

        Assert.IsEmpty(_hotkeys.ActiveCommandHotkeys);
        Assert.IsNull(command.Hotkey);
        Assert.IsEmpty(command.Tags);
    }

    [TestMethod]
    public void Dispose_UnsubscribesFromSettingsAndCommands()
    {
        var active = _hotkeys.ActiveCommandHotkeys;
        _hotkeys.UpdateHotkey(CommandId, null);
        _hotkeys.Dispose();

        _settingsService.Object.UpdateSettings(settings => settings with { CommandHotkeys = [] });
        _commands.TopLevelCommands.Clear();
        _uiScheduler.ExecuteAll();

        Assert.AreSame(active, _hotkeys.ActiveCommandHotkeys);
    }

    /// <summary>Models ShortcutControl's record comparison and the generated two-way binding's initial and subsequent echoes.</summary>
    private sealed class ShortcutPickerBinding : IDisposable
    {
        private readonly TopLevelViewModel _command;

        public ShortcutPickerBinding(TopLevelViewModel command)
        {
            _command = command;
            _command.PropertyChanged += CommandPropertyChanged;
            SetHotkey(_command.Hotkey);
        }

        public HotkeySettings? Hotkey { get; private set; }

        public void SetHotkey(HotkeySettings? value)
        {
            if (Hotkey != value)
            {
                Hotkey = value;
                _command.Hotkey = value;
            }
        }

        public void Dispose()
        {
            _command.PropertyChanged -= CommandPropertyChanged;
            GC.SuppressFinalize(this);
        }

        private void CommandPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(TopLevelViewModel.Hotkey))
            {
                SetHotkey(_command.Hotkey);
            }
        }
    }

    /// <summary>Waits for initialization notifications queued before it to reach their UI schedulers.</summary>
    private sealed class BatchUpdateBarrier : IBatchUpdateTarget
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BatchUpdateBarrier()
        {
            BatchUpdateManager.Queue(this);
        }

        public Task Completion => _completion.Task;

        public TaskScheduler UIScheduler => TaskScheduler.Default;

        public bool TryMarkBatchQueued() => true;

        public void ClearBatchQueued()
        {
        }

        public void ApplyPendingUpdates() => _completion.SetResult();
    }

    private sealed class QueuedTaskScheduler : TaskScheduler
    {
        private readonly ConcurrentQueue<Task> _tasks = [];

        public int PendingCount => _tasks.Count;

        public bool RejectNextTask { get; set; }

        protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();

        protected override void QueueTask(Task task)
        {
            if (RejectNextTask)
            {
                RejectNextTask = false;
                throw new InvalidOperationException("Hotkey display scheduler test failure");
            }

            _tasks.Enqueue(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        public void ExecuteAll()
        {
            while (_tasks.TryDequeue(out var task))
            {
                TryExecuteTask(task);
                task.GetAwaiter().GetResult();
            }
        }
    }

    private sealed partial class TestCommandProvider : CommandProvider
    {
        private readonly string _commandId;

        public TestCommandProvider(string providerId = ProviderId, string commandId = CommandId)
        {
            Id = providerId;
            DisplayName = "Test provider";
            _commandId = commandId;
        }

        public override ICommandItem[] TopLevelCommands() => [new CommandItem(new NoOpCommand { Id = _commandId })];

        public override ICommandItem? GetCommandItem(string id) => id == _commandId ? new CommandItem(new NoOpCommand { Id = _commandId }) : null;

        public override IFallbackCommandItem[] FallbackCommands() => [new FallbackCommandItem(new NoOpCommand(), "Test fallback", FallbackId)];
    }
}
