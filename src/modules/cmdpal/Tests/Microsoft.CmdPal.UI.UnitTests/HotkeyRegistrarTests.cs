// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public sealed class HotkeyRegistrarTests
{
    [TestMethod]
    public void AddingAndRemovingCommand_PreservesUnchangedRegistrations()
    {
        var harness = new Harness();
        var settings = new SettingsModel { EnableDock = true };
        var baseline = new HotkeyRegistration(settings, []);
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        var loaded = new HotkeyRegistration(settings, [new(key, "command")]);
        harness.Registrar.Update(baseline);
        var activationId = harness.GetId(settings.Hotkey!);
        var initialAttempts = harness.Attempts.Count;

        harness.Registrar.Update(loaded);
        var commandId = harness.GetId(key);
        harness.Registrar.Update(loaded);

        Assert.AreEqual(initialAttempts + 1, harness.Attempts.Count);
        Assert.IsEmpty(harness.Removed);
        Assert.AreEqual(activationId, harness.GetId(settings.Hotkey!));

        harness.Registrar.Update(baseline);

        Assert.HasCount(1, harness.Removed);
        Assert.AreEqual(commandId, harness.Removed[0]);
        Assert.AreEqual(activationId, harness.GetId(settings.Hotkey!));
        Assert.IsNull(harness.Registrar.GetCommand(commandId, Message(key)));
    }

    [TestMethod]
    public void ReassignedKey_UnregistersOldIdBeforeRegisteringNewTarget()
    {
        var harness = new Harness();
        var settings = new SettingsModel();
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        harness.Registrar.Update(new HotkeyRegistration(settings, [new(key, "original")]));
        var oldId = harness.GetId(key);

        var replacement = new HotkeyRegistration(settings, [new(key, "replacement")]);
        harness.Registrar.Update(replacement);
        var newId = harness.GetId(key);
        var attempts = harness.Attempts.Count;
        harness.Registrar.Update(replacement);

        Assert.AreNotEqual(oldId, newId);
        CollectionAssert.Contains(harness.Removed, oldId);
        Assert.IsNull(harness.Registrar.GetCommand(oldId, Message(key)));
        Assert.AreEqual("replacement", harness.Registrar.GetCommand(newId, Message(key)));
        Assert.AreEqual(attempts, harness.Attempts.Count);
    }

    [TestMethod]
    public void FailedKey_IsRetriedWithSameIdWithoutReleasingSuccessfulKeys()
    {
        var harness = new Harness();
        var settings = new SettingsModel { EnableDock = true };
        var desired = new HotkeyRegistration(settings, []);
        harness.Failures.Add(settings.Hotkey!);
        harness.Registrar.Update(desired);
        var failedId = harness.Attempts.Single(attempt => attempt.Key == settings.Hotkey).Id;
        var successful = harness.Native.Single();

        harness.Failures.Clear();
        harness.Registrar.Update(desired);
        harness.Registrar.Update(desired);

        Assert.HasCount(3, harness.Attempts);
        Assert.IsEmpty(harness.Removed);
        Assert.AreEqual(failedId, harness.GetId(settings.Hotkey!));
        Assert.AreEqual(successful.Value, harness.Native[successful.Key]);
        Assert.AreEqual(string.Empty, harness.Registrar.GetCommand(failedId, Message(settings.Hotkey!)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ModeSwitch_RemovesOldBackendMappingsBeforeAddingNewOnes(bool startWithHook)
    {
        var harness = new Harness();
        var settings = new SettingsModel { EnableDock = true };
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        foreach (var useHook in new[] { startWithHook, !startWithHook, startWithHook })
        {
            var desired = new HotkeyRegistration(settings with { UseLowLevelGlobalHotkey = useHook }, [new(key, "command")]);
            harness.Registrar.Update(desired);
            harness.Registrar.Update(desired);

            Assert.AreEqual(useHook ? 0 : 3, harness.Native.Count);
            Assert.AreEqual(useHook ? 3 : 0, harness.Hook.Count);
            if (useHook)
            {
                foreach (var attempt in harness.Attempts)
                {
                    Assert.IsNull(harness.Registrar.GetCommand(attempt.Id, Message(attempt.Key)));
                }
            }
            else
            {
                Assert.AreEqual("command", harness.Registrar.GetCommand(harness.GetId(key), Message(key)));
            }
        }
    }

    [TestMethod]
    public void ReaddedBinding_ReusesItsOriginalId()
    {
        var harness = new Harness();
        var settings = new SettingsModel();
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        var loaded = new HotkeyRegistration(settings, [new(key, "command")]);
        harness.Registrar.Update(loaded);
        var originalId = harness.GetId(key);

        harness.Registrar.Update(new HotkeyRegistration(settings, []));
        harness.Registrar.Update(loaded);

        Assert.AreEqual(originalId, harness.GetId(key));
        Assert.AreEqual("command", harness.Registrar.GetCommand(originalId, Message(key)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DisabledShortcuts_DoNotReachEitherBackend(bool useHook)
    {
        var harness = new Harness();
        var settings = new SettingsModel
        {
            Hotkey = new HotkeySettings(),
            EnableDock = true,
            DockFocusHotkey = new HotkeySettings(),
            UseLowLevelGlobalHotkey = useHook,
        };
        var disabled = new HotkeyRegistration(settings, [new(new HotkeySettings(), "command")]);

        harness.Registrar.Update(disabled);
        harness.Registrar.Update(disabled);

        Assert.IsEmpty(harness.Attempts);
        Assert.IsEmpty(harness.Native);
        Assert.IsEmpty(harness.Hook);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BareFunctionKey_ReachesSelectedBackend(bool useHook)
    {
        var harness = new Harness();
        var settings = new SettingsModel { Hotkey = null, EnableDock = false, UseLowLevelGlobalHotkey = useHook };
        var key = new HotkeySettings { Code = 124 };
        var registration = new HotkeyRegistration(settings, [new(key, "command")]);

        harness.Registrar.Update(registration);
        harness.Registrar.Update(registration);

        if (useHook)
        {
            Assert.IsEmpty(harness.Attempts);
            Assert.HasCount(1, harness.Hook);
            Assert.AreEqual("command", harness.Hook[key]);
        }
        else
        {
            Assert.HasCount(1, harness.Attempts);
            Assert.IsEmpty(harness.Hook);
            Assert.AreEqual("command", harness.Registrar.GetCommand(harness.GetId(key), Message(key)));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [DoNotParallelize]
    public void DuplicateWarning_OnlyLogsNewOrReturningConflicts(bool useHook)
    {
        var harness = new Harness();
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        var settings = new SettingsModel { Hotkey = key, EnableDock = false, UseLowLevelGlobalHotkey = useHook };
        ImmutableList<TopLevelHotkey> bindings = [new(key, "skipped-command")];
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var listener = new TextWriterTraceListener(output);
        Trace.Listeners.Add(listener);
        try
        {
            var registration = new HotkeyRegistration(settings, bindings);
            listener.Flush();
            Assert.AreEqual(string.Empty, output.ToString(), "Building a snapshot must not log warnings.");
            harness.Registrar.Update(registration);
            listener.Flush();
            StringAssert.Contains(output.ToString(), "for 'skipped-command'; already assigned to ''");
            output.GetStringBuilder().Clear();

            harness.Registrar.Update(new HotkeyRegistration(settings, bindings));
            harness.Registrar.Update(new HotkeyRegistration(settings, [new(key with { Key = "A" }, "skipped-command")]));
            harness.Registrar.Update(new HotkeyRegistration(settings, bindings.Add(new(key with { Code = 66 }, "unrelated-command"))));
            listener.Flush();
            Assert.AreEqual(string.Empty, output.ToString());

            harness.Registrar.Update(new HotkeyRegistration(settings, bindings.Add(new(key, "new-conflict"))));
            listener.Flush();
            StringAssert.Contains(output.ToString(), "for 'new-conflict'");
            Assert.IsFalse(output.ToString().Contains("for 'skipped-command'", StringComparison.Ordinal));
            output.GetStringBuilder().Clear();

            harness.Registrar.Update(new HotkeyRegistration(settings, []));
            harness.Registrar.Update(new HotkeyRegistration(settings, bindings));
            listener.Flush();
            StringAssert.Contains(output.ToString(), "for 'skipped-command'");
            output.GetStringBuilder().Clear();

            harness.Registrar.Update(new HotkeyRegistration(settings with { Hotkey = null }, [new(key, "new-owner"), .. bindings]));
            listener.Flush();
            StringAssert.Contains(output.ToString(), "for 'skipped-command'; already assigned to 'new-owner'");
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void NativeRetry_DoesNotRepeatDuplicateWarning()
    {
        var harness = new Harness();
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        var settings = new SettingsModel { Hotkey = key, EnableDock = false };
        harness.Failures.Add(key);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var listener = new TextWriterTraceListener(output);
        Trace.Listeners.Add(listener);
        try
        {
            harness.Registrar.Update(new HotkeyRegistration(settings, [new(key, "skipped-command")]));
            listener.Flush();
            StringAssert.Contains(output.ToString(), "Skipping duplicate hotkey");
            output.GetStringBuilder().Clear();

            harness.Registrar.Update(new HotkeyRegistration(settings, [new(key, "skipped-command")]));
            harness.Failures.Clear();
            harness.Registrar.Update(new HotkeyRegistration(settings, [new(key, "skipped-command")]));
            listener.Flush();

            Assert.AreEqual(string.Empty, output.ToString());
            Assert.HasCount(3, harness.Attempts);
            Assert.HasCount(1, harness.Native);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [DoNotParallelize]
    public void NativeFailureWarning_OnlyLogsNewOrReturningFailures(bool resetWithHookMode)
    {
        var harness = new Harness();
        var key = new HotkeySettings { Ctrl = true, Code = 65 };
        var settings = new SettingsModel { Hotkey = null, EnableDock = false };
        ImmutableList<TopLevelHotkey> bindings = [new(key, "blocked-command")];
        harness.Failures.Add(key);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var listener = new TextWriterTraceListener(output);
        Trace.Listeners.Add(listener);
        try
        {
            harness.Registrar.Update(new HotkeyRegistration(settings, bindings));
            listener.Flush();
            StringAssert.Contains(output.ToString(), "for 'blocked-command'. Error: 1409.");
            output.GetStringBuilder().Clear();

            harness.Registrar.Update(new HotkeyRegistration(settings, bindings));
            harness.Registrar.Update(new HotkeyRegistration(settings, bindings.Add(new(key with { Code = 66 }, "other-command"))));
            listener.Flush();
            Assert.AreEqual(string.Empty, output.ToString());
            Assert.AreEqual(3, harness.Attempts.Count(attempt => attempt.Key == key));

            harness.Registrar.Update(new HotkeyRegistration(settings with { UseLowLevelGlobalHotkey = resetWithHookMode }, resetWithHookMode ? bindings : []));
            harness.Registrar.Update(new HotkeyRegistration(settings, bindings));
            listener.Flush();
            StringAssert.Contains(output.ToString(), "for 'blocked-command'. Error: 1409.");
            output.GetStringBuilder().Clear();

            harness.Registrar.Update(new HotkeyRegistration(settings, [new(key, "replacement-command")]));
            listener.Flush();
            StringAssert.Contains(output.ToString(), "for 'replacement-command'. Error: 1409.");
            output.GetStringBuilder().Clear();

            harness.Failures.Clear();
            harness.Registrar.Update(new HotkeyRegistration(settings, [new(key, "replacement-command")]));
            listener.Flush();
            Assert.AreEqual(string.Empty, output.ToString());
            Assert.AreEqual("replacement-command", harness.Registrar.GetCommand(harness.GetId(key), Message(key)));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    private static nint Message(HotkeySettings key) => (key.Code << 16) | (int)HotkeyWindowMessageHelper.GetModifiers(key);

    private sealed class Harness
    {
        public Dictionary<int, HotkeySettings> Native { get; } = [];

        public Dictionary<HotkeySettings, string> Hook { get; } = [];

        public List<(int Id, HotkeySettings Key)> Attempts { get; } = [];

        public List<int> Removed { get; } = [];

        public HashSet<HotkeySettings> Failures { get; } = [];

        public HotkeyRegistrar Registrar { get; }

        public Harness()
        {
            Registrar = new HotkeyRegistrar(
                (id, key) =>
                {
                    Attempts.Add((id, key));
                    if (Failures.Contains(key))
                    {
                        return (false, 1409);
                    }

                    Assert.IsFalse(Native.ContainsValue(key), "The previous registration must be removed first.");
                    Assert.IsFalse(Hook.ContainsKey(key));
                    Native.Add(id, key);
                    return (true, 0);
                },
                id =>
                {
                    Assert.IsTrue(Native.Remove(id));
                    Removed.Add(id);
                },
                (key, commandId) =>
                {
                    Assert.IsFalse(Native.ContainsValue(key));
                    Assert.IsTrue(Hook.TryAdd(key, commandId), "A stale hook mapping must not block the new command.");
                },
                key => Assert.IsTrue(Hook.Remove(key)));
        }

        public int GetId(HotkeySettings key) => Native.Single(hotkey => hotkey.Value == key).Key;
    }
}
