// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public partial class GlobalHotkeyServiceTests
{
    [TestMethod]
    public void DisabledLowLevelModeNeverInstallsHook()
    {
        using var harness = new Harness();
        harness.Service.Configure(false, [Shortcut("root")]);

        Assert.AreEqual(0, harness.Listener.Starts);
        Assert.AreEqual(1, harness.Standard.Count);
    }

    [TestMethod]
    public void EmptyShortcutsNeverInstallHook()
    {
        using var harness = new Harness();
        harness.Service.Configure(true, [new(null, "missing"), new(new HotkeySettings(), "empty")]);

        Assert.AreEqual(0, harness.Listener.Starts);
        Assert.AreEqual(0, harness.Standard.Count);
    }

    [TestMethod]
    public void InstallationFailureFallsBackAndReportsOnce()
    {
        using var harness = new Harness();
        harness.Listener.Error = new Win32Exception(5);
        Assert.IsTrue(harness.Service.Configure(true, [Shortcut("root")]));

        Assert.AreEqual(1, harness.Listener.Starts);
        Assert.IsFalse(harness.Listener.Running);
        Assert.IsTrue(harness.Service.IsUsingFallback);
        Assert.AreEqual(1, harness.Standard.Count);
        Assert.AreEqual("root", harness.Standard[0].CommandId);
        Assert.AreEqual(1, harness.Errors.Count);
        Assert.AreEqual("Global keyboard hook failed; using standard shortcut registration", harness.LogMessages.Single());
        Assert.IsFalse(harness.Notifications.Single());

        Assert.IsFalse(harness.Service.Configure(true, [Shortcut("root")]));
        Assert.IsTrue(harness.Service.Configure(true, [Shortcut("changed")]));
        Assert.AreEqual(1, harness.Listener.Starts);
        Assert.AreEqual(1, harness.Errors.Count);
        Assert.AreEqual("changed", harness.Standard[0].CommandId);
    }

    [TestMethod]
    public void ChangingUnrelatedSettingsOrKeyDisplayNameDoesNotReregister()
    {
        using var harness = new Harness();
        Assert.IsTrue(harness.Service.Configure(true, [Shortcut("root")]));
        var namedKey = new HotkeySettings(false, true, true, false, 0x4B) { Key = "Localized key name" };

        Assert.IsFalse(harness.Service.Configure(true, [new(namedKey, "root")]));
        Assert.AreEqual(1, harness.Listener.Starts);
        Assert.AreEqual(1, harness.Listener.Configurations);
        Assert.AreEqual(1, harness.Clears);
    }

    [TestMethod]
    public void TogglingModeAllowsExplicitRetryWithoutRepeatingNotification()
    {
        using var harness = new Harness();
        harness.Listener.Error = new Win32Exception(5);
        harness.Service.Configure(true, [Shortcut("root")]);
        harness.Service.Configure(false, [Shortcut("root")]);
        harness.Service.Configure(true, [Shortcut("root")]);

        Assert.AreEqual(2, harness.Listener.Starts);
        Assert.AreEqual(2, harness.Errors.Count);
        Assert.IsFalse(harness.Notifications.Single());

        harness.Service.Configure(false, [Shortcut("root")]);
        harness.Listener.Error = null;
        harness.Service.Configure(true, [Shortcut("root")]);
        Assert.IsTrue(harness.Listener.Running);
        Assert.IsFalse(harness.Service.IsUsingFallback);
        Assert.AreEqual(0, harness.Standard.Count);
    }

    [TestMethod]
    public void RuntimeFailureFallsBackAndDoesNotRetryOnSettingsSave()
    {
        using var harness = new Harness();
        harness.Service.Configure(true, [Shortcut("root")]);
        harness.Service.HandleHookFailure(new Win32Exception(5));
        harness.Service.Configure(true, [Shortcut("changed")]);

        Assert.AreEqual(1, harness.Listener.Starts);
        Assert.IsFalse(harness.Listener.Running);
        Assert.AreEqual("changed", harness.Standard[0].CommandId);
        Assert.AreEqual(1, harness.Errors.Count);
        Assert.AreEqual("Global keyboard hook failed; using standard shortcut registration", harness.LogMessages.Single());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RuntimeFailureStillFallsBackWhenUnregistrationFails(bool summonUnavailable)
    {
        using var harness = new Harness();
        var hookFailure = new Win32Exception(5);
        var unregisterFailure = new InvalidOperationException("Hotkey unregistration failed");
        TopLevelHotkey[] hotkeys = [Shortcut(string.Empty), Shortcut("command", 0x4C)];
        if (summonUnavailable)
        {
            harness.RejectedCommands.Add(string.Empty);
        }

        harness.Service.Configure(true, hotkeys);
        harness.NextUnregisterError = unregisterFailure;
        harness.Service.HandleHookFailure(hookFailure);

        string[] expectedCommands = summonUnavailable ? ["command"] : [string.Empty, "command"];
        string[] expectedLogs = ["Failed to unregister global hotkeys", "Global keyboard hook failed; using standard shortcut registration"];
        CollectionAssert.AreEqual(expectedCommands, harness.Standard.Select(hotkey => hotkey.CommandId).ToArray());
        CollectionAssert.AreEqual(new Exception[] { unregisterFailure, hookFailure }, harness.Errors);
        CollectionAssert.AreEqual(expectedLogs, harness.LogMessages);
        Assert.IsTrue(harness.Service.IsUsingFallback);
        Assert.AreEqual(summonUnavailable, harness.Service.IsSummonHotkeyUnavailable);
        Assert.AreEqual(summonUnavailable, harness.Notifications.Single());

        Assert.IsFalse(harness.Service.Configure(true, hotkeys));
        harness.Service.HandleHookFailure(hookFailure);
        Assert.AreEqual(2, harness.Errors.Count);
        Assert.AreEqual(1, harness.Notifications.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ConfigureAppliesNewSettingsWhenUnregistrationFails(bool useLowLevel)
    {
        using var harness = new Harness();
        var failure = new InvalidOperationException("Hotkey unregistration failed");
        harness.NextUnregisterError = failure;

        Assert.IsTrue(harness.Service.Configure(useLowLevel, [Shortcut("root")]));

        Assert.AreSame(failure, harness.Errors.Single());
        Assert.AreEqual("Failed to unregister global hotkeys", harness.LogMessages.Single());
        Assert.AreEqual(useLowLevel, harness.Listener.Running);
        var registrations = useLowLevel ? harness.Listener.Hotkeys : harness.Standard;
        Assert.AreEqual("root", registrations.Single().CommandId);
        Assert.AreEqual(0, harness.Notifications.Count);
        Assert.IsFalse(harness.Service.Configure(useLowLevel, [Shortcut("root")]));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ConfigureUpdatesRunningListenerDespiteUnregistrationFailure(bool useLowLevel)
    {
        using var harness = new Harness();
        harness.Service.Configure(true, [Shortcut("old")]);
        var failure = new InvalidOperationException("Hotkey unregistration failed");
        harness.NextUnregisterError = failure;

        Assert.IsTrue(harness.Service.Configure(useLowLevel, [Shortcut("changed", 0x4C)]));
        Assert.AreSame(failure, harness.Errors.Single());
        Assert.AreEqual(useLowLevel, harness.Listener.Running);
        var registrations = useLowLevel ? harness.Listener.Hotkeys : harness.Standard;
        Assert.AreEqual("changed", registrations.Single().CommandId);
        Assert.AreEqual(0x4C, registrations.Single().Hotkey!.Code);
        Assert.IsFalse(harness.Service.Configure(useLowLevel, [Shortcut("changed", 0x4C)]));

        var hookFailure = new Win32Exception(5);
        harness.Service.HandleHookFailure(hookFailure);
        Assert.IsFalse(harness.Listener.Running);
        Assert.AreEqual("changed", harness.Standard.Single().CommandId);
        Assert.AreEqual(useLowLevel, harness.Service.IsUsingFallback);
        Assert.AreEqual(useLowLevel ? 1 : 0, harness.Notifications.Count);
        if (useLowLevel)
        {
            Assert.AreSame(hookFailure, harness.Errors.Last());
            Assert.IsFalse(harness.Notifications.Single());
        }
    }

    [TestMethod]
    public void StopCancelsRegistrationAndIgnoresLateFailure()
    {
        using var harness = new Harness();
        harness.Service.Configure(true, [Shortcut("root")]);
        harness.Service.Stop();
        harness.Service.HandleHookFailure(new Win32Exception(5));

        Assert.IsFalse(harness.Listener.Running);
        Assert.AreEqual(0, harness.Standard.Count);
        Assert.AreEqual(0, harness.Errors.Count);
        Assert.IsFalse(harness.Listener.Disposed);

        harness.Service.Configure(true, [Shortcut("root")]);
        Assert.AreEqual(2, harness.Listener.Starts);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void StopHandlesUnregistrationFailureAndAllowsRestart(bool useLowLevel, bool failLogging)
    {
        using var harness = new Harness();
        harness.Service.Configure(useLowLevel, [Shortcut("root")]);
        var clears = harness.Clears;
        var failure = new InvalidOperationException("Hotkey unregistration failed");
        harness.NextUnregisterError = failure;
        harness.LoggingError = failLogging ? new IOException("Log writer failed") : null;

        harness.Service.Stop();

        Assert.AreSame(failure, harness.Errors.Single());
        Assert.AreEqual("Failed to unregister global hotkeys", harness.LogMessages.Single());
        Assert.AreEqual(clears + 1, harness.Clears);
        Assert.IsFalse(harness.Listener.Running);
        Assert.IsFalse(harness.Listener.Disposed);
        harness.Calls.Clear();
        harness.Service.HandleHookFailure(new Win32Exception(5));
        Assert.AreEqual(0, harness.Calls.Count);
        Assert.AreSame(failure, harness.Errors.Single());
        Assert.AreEqual(0, harness.Notifications.Count);

        Assert.IsTrue(harness.Service.Configure(useLowLevel, [Shortcut("root")]));
        Assert.AreEqual(useLowLevel, harness.Listener.Running);
        Assert.AreEqual(useLowLevel ? 0 : 1, harness.Standard.Count);
        Assert.AreEqual(useLowLevel ? 2 : 0, harness.Listener.Starts);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void DisposeClearsRegistrationsAndIgnoresLateFailure(bool useLowLevel, bool failHook)
    {
        using var harness = new Harness();
        harness.Listener.Error = failHook ? new Win32Exception(5) : null;
        harness.Service.Configure(useLowLevel, [Shortcut("root")]);
        var clears = harness.Clears;
        var errors = harness.Errors.Count;
        harness.Calls.Clear();

        harness.Service.Dispose();

        var stop = harness.Calls.IndexOf("stop");
        Assert.IsTrue(stop >= 0 && stop < harness.Calls.IndexOf("clear"), "The listener must stop before standard hotkeys are cleared.");
        Assert.IsTrue(harness.Listener.Disposed);
        Assert.AreEqual(clears + 1, harness.Clears);
        Assert.IsFalse(harness.Listener.Running);
        Assert.IsFalse(harness.Service.IsUsingFallback);
        Assert.AreEqual(0, harness.Standard.Count);

        harness.Calls.Clear();
        harness.Service.Dispose();
        harness.Service.HandleHookFailure(new Win32Exception(5));

        Assert.AreEqual(errors, harness.Errors.Count);
        Assert.ThrowsExactly<ObjectDisposedException>(() => harness.Service.Configure(useLowLevel, [Shortcut("root")]));
        Assert.AreEqual(0, harness.Calls.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void StopAfterDisposeDoesNotTouchListenerOrRegistrations(bool useLowLevel)
    {
        using var harness = new Harness();
        harness.Service.Configure(useLowLevel, [Shortcut("root")]);
        harness.Service.Dispose();
        harness.Calls.Clear();

        harness.Service.Stop();
        harness.Service.Stop();

        Assert.AreEqual(0, harness.Calls.Count);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void DisposeFinishesListenerCleanupWhenUnregistrationFails(bool useLowLevel, bool failLogging)
    {
        using var harness = new Harness();
        harness.Service.Configure(useLowLevel, [Shortcut("root")]);
        var clears = harness.Clears;
        var failure = new InvalidOperationException("Hotkey unregistration failed");
        harness.NextUnregisterError = failure;
        harness.LoggingError = failLogging ? new IOException("Log writer failed") : null;
        harness.Calls.Clear();

        harness.Service.Dispose();

        Assert.AreSame(failure, harness.Errors.Single());
        Assert.AreEqual("Failed to unregister global hotkeys", harness.LogMessages.Single());
        Assert.AreEqual(clears + 1, harness.Clears);
        Assert.IsTrue(harness.Calls.IndexOf("stop") < harness.Calls.IndexOf("clear"));
        Assert.IsTrue(harness.Calls.IndexOf("clear") < harness.Calls.IndexOf("dispose"));
        Assert.IsTrue(harness.Listener.Disposed);
        Assert.IsFalse(harness.Listener.Running);
        harness.Calls.Clear();

        harness.Service.Stop();
        harness.Service.Dispose();

        Assert.AreEqual(0, harness.Calls.Count);
    }

    [TestMethod]
    public void RestartAfterFallbackDoesNotRetryFailedHook()
    {
        using var harness = new Harness();
        harness.Listener.Error = new Win32Exception(5);
        harness.Service.Configure(true, [Shortcut("root")]);
        harness.Service.Stop();
        harness.Service.Configure(true, [Shortcut("root")]);

        Assert.AreEqual(1, harness.Listener.Starts);
        Assert.AreEqual(1, harness.Standard.Count);
        Assert.AreEqual(1, harness.Errors.Count);
    }

    [TestMethod]
    public void DisablingLowLevelModeStopsHookAndUsesStandardRegistration()
    {
        using var harness = new Harness();
        harness.Service.Configure(true, [Shortcut("root")]);
        Assert.IsTrue(harness.Listener.Running);

        harness.Service.Configure(false, [Shortcut("root")]);

        Assert.IsFalse(harness.Listener.Running);
        Assert.AreEqual(1, harness.Listener.Starts);
        Assert.AreEqual(1, harness.Standard.Count);
    }

    [TestMethod]
    public void DuplicateShortcutPreservesFirstCommandInBothModes()
    {
        using var harness = new Harness();
        harness.Service.Configure(true, [Shortcut("first"), Shortcut("second")]);
        Assert.AreEqual(1, harness.Listener.Hotkeys.Count);
        Assert.AreEqual("first", harness.Listener.Hotkeys[0].CommandId);

        harness.Service.Configure(false, [Shortcut("first"), Shortcut("second")]);
        Assert.AreEqual(1, harness.Standard.Count);
        Assert.AreEqual("first", harness.Standard[0].CommandId);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InvalidHotkeysAreFilteredBeforeEitherRegistrationPath(bool useLowLevel)
    {
        using var harness = new Harness();
        harness.Service.Configure(useLowLevel, [
            new(null, "missing"),
            Shortcut("empty", 0),
            Shortcut("negative", -1),
            Shortcut("too large", 256),
            Shortcut("minimum", 1),
            Shortcut("maximum", 255),
        ]);

        var registered = useLowLevel ? harness.Listener.Hotkeys : harness.Standard;
        string[] expectedCommands = ["minimum", "maximum"];
        CollectionAssert.AreEqual(expectedCommands, registered.Select(hotkey => hotkey.CommandId).ToArray());
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void FailedSummonFallbackRequestsImmediateWarning(bool runtimeFailure, bool failLogging)
    {
        using var harness = new Harness();
        harness.LoggingError = failLogging ? new IOException("Log writer failed") : null;
        harness.RejectedCommands.Add(string.Empty);
        var error = new Win32Exception(5);
        if (!runtimeFailure)
        {
            harness.Listener.Error = error;
        }

        harness.Service.Configure(true, [Shortcut(string.Empty)]);
        if (runtimeFailure)
        {
            harness.Service.HandleHookFailure(error);
        }

        Assert.IsTrue(harness.Service.IsSummonHotkeyUnavailable);
        Assert.IsTrue(harness.Notifications.Single());
        Assert.AreSame(error, harness.Errors.Single());
        Assert.AreEqual(0, harness.Standard.Count);

        Assert.IsFalse(harness.Service.Configure(true, [Shortcut(string.Empty)]));
        Assert.AreEqual(1, harness.Notifications.Count);
    }

    [TestMethod]
    public void WorkingSummonFallbackAllowsDeferredWarningDespiteFailedCommandHotkey()
    {
        using var harness = new Harness();
        harness.Listener.Error = new Win32Exception(5);
        harness.RejectedCommands.Add("command");
        harness.Service.Configure(true, [Shortcut(string.Empty), Shortcut("command", 0x4C)]);

        Assert.IsFalse(harness.Service.IsSummonHotkeyUnavailable);
        Assert.IsFalse(harness.Notifications.Single());
        Assert.AreEqual(string.Empty, harness.Standard.Single().CommandId);
    }

    [TestMethod]
    public void LosingSummonFallbackEscalatesPreviouslyDeferredWarningOnlyOnce()
    {
        using var harness = new Harness();
        harness.Listener.Error = new Win32Exception(5);
        harness.Service.Configure(true, [Shortcut(string.Empty)]);
        harness.RejectedCommands.Add(string.Empty);
        harness.Service.Configure(true, [Shortcut(string.Empty, 0x4C)]);
        harness.Service.Configure(true, [Shortcut(string.Empty, 0x4D)]);

        Assert.AreEqual(2, harness.Notifications.Count);
        Assert.IsFalse(harness.Notifications[0]);
        Assert.IsTrue(harness.Notifications[1]);
        Assert.AreEqual(1, harness.Errors.Count);
        Assert.AreEqual(1, harness.Listener.Starts);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UnchangedSettingsRetryOnlyFailedStandardHotkeys(bool useLowLevel)
    {
        using var harness = new Harness();
        harness.Listener.Error = new Win32Exception(5);
        harness.RejectedCommands.Add(string.Empty);
        TopLevelHotkey[] hotkeys = [Shortcut(string.Empty), Shortcut("command", 0x4C)];
        harness.Service.Configure(useLowLevel, hotkeys);
        Assert.AreEqual("command", harness.Standard.Single().CommandId);

        Assert.IsFalse(harness.Service.Configure(useLowLevel, hotkeys));
        harness.RejectedCommands.Clear();
        Assert.IsTrue(harness.Service.Configure(useLowLevel, hotkeys));
        Assert.IsFalse(harness.Service.Configure(useLowLevel, hotkeys));

        CollectionAssert.AreEqual(new[] { string.Empty, "command", string.Empty, string.Empty }, harness.Attempts);
        Assert.AreEqual(2, harness.Standard.Count);
        Assert.AreEqual(1, harness.Clears);
        Assert.AreEqual(useLowLevel ? 1 : 0, harness.Listener.Starts);
        Assert.AreEqual(useLowLevel ? 1 : 0, harness.Notifications.Count);
        Assert.IsFalse(harness.Service.IsSummonHotkeyUnavailable);
    }

    [TestMethod]
    public void ChangedSettingsDiscardObsoleteFailedRegistrations()
    {
        using var harness = new Harness();
        harness.RejectedCommands.Add("removed");
        harness.Service.Configure(false, [Shortcut(string.Empty), Shortcut("removed", 0x4C)]);
        harness.Service.Configure(false, [Shortcut(string.Empty)]);
        harness.RejectedCommands.Clear();
        Assert.IsFalse(harness.Service.Configure(false, [Shortcut(string.Empty)]));

        CollectionAssert.AreEqual(new[] { string.Empty, "removed", string.Empty }, harness.Attempts);
        Assert.AreEqual(string.Empty, harness.Standard.Single().CommandId);
    }

    private static TopLevelHotkey Shortcut(string commandId, int code = 0x4B) => new(new HotkeySettings(false, true, true, false, code), commandId);

    private sealed partial class Harness : IDisposable
    {
        public Harness()
        {
            Listener = new FakeListener(Calls);
            Service = new GlobalHotkeyService(
                Listener,
                () =>
                {
                    Calls.Add("clear");
                    Clears++;
                    if (NextUnregisterError is { } error)
                    {
                        NextUnregisterError = null;
                        throw error;
                    }

                    Standard.Clear();
                },
                hotkey =>
                {
                    Attempts.Add(hotkey.CommandId);
                    if (RejectedCommands.Contains(hotkey.CommandId))
                    {
                        return false;
                    }

                    Standard.Add(hotkey);
                    return true;
                },
                (message, error) =>
                {
                    LogMessages.Add(message);
                    Errors.Add(error);
                    if (LoggingError is { } loggingError)
                    {
                        throw loggingError;
                    }
                },
                Notifications.Add);
        }

        public FakeListener Listener { get; }

        public List<string> Calls { get; } = [];

        public List<TopLevelHotkey> Standard { get; } = [];

        public List<Exception> Errors { get; } = [];

        public List<string> LogMessages { get; } = [];

        public List<bool> Notifications { get; } = [];

        public HashSet<string> RejectedCommands { get; } = [];

        public List<string> Attempts { get; } = [];

        public GlobalHotkeyService Service { get; }

        public int Clears { get; private set; }

        public Exception? NextUnregisterError { get; set; }

        public Exception? LoggingError { get; set; }

        public void Dispose() => Service.Dispose();
    }

    private sealed partial class FakeListener : IGlobalKeyboardListener
    {
        private readonly List<string> _calls;

        public FakeListener(List<string> calls)
        {
            _calls = calls;
        }

        public int Starts { get; private set; }

        public int Configurations { get; private set; }

        public bool Running { get; private set; }

        public bool Disposed { get; private set; }

        public Exception? Error { get; set; }

        public List<TopLevelHotkey> Hotkeys { get; private set; } = [];

        public bool Start(out Exception? error)
        {
            _calls.Add("start");
            ObjectDisposedException.ThrowIf(Disposed, this);
            Starts++;
            if (Running)
            {
                error = null;
                return true;
            }

            Stop();
            error = Error;
            Running = error is null;
            return Running;
        }

        public void Stop()
        {
            _calls.Add("stop");
            Running = false;
        }

        public void SetHotkeys(IEnumerable<TopLevelHotkey> hotkeys)
        {
            _calls.Add("set-hotkeys");
            ObjectDisposedException.ThrowIf(Disposed, this);
            Configurations++;
            Hotkeys = [.. hotkeys];
        }

        public void Dispose()
        {
            _calls.Add("dispose");
            if (Disposed)
            {
                return;
            }

            Disposed = true;
            Stop();
        }
    }
}
