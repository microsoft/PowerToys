// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public partial class GlobalKeyboardListenerTests
{
    [TestMethod]
    [DataRow(false, true, false, true, 0x4B, true)]
    [DataRow(true, true, false, true, 0x4B, false)]
    [DataRow(false, false, false, true, 0x4B, false)]
    [DataRow(false, true, true, true, 0x4B, false)]
    [DataRow(false, true, false, false, 0x4B, false)]
    [DataRow(false, true, false, true, 0x4C, false)]
    public void CallbackRequiresExactKeyAndModifiers(bool win, bool ctrl, bool shift, bool alt, int key, bool expected)
    {
        using var harness = new Harness();
        harness.Listener.SetHotkeyAction(false, true, false, true, 0x4B, "command");
        harness.Hook.SetModifiers(win, ctrl, shift, alt);

        Assert.AreEqual(expected ? 1 : 42, harness.Hook.Key((uint)key));
        Assert.AreEqual(expected ? 1 : 0, harness.Commands.Count);
        Assert.AreEqual(expected ? 1 : 0, harness.Hook.DummyKeys);
        Assert.AreEqual(expected ? 0 : 1, harness.Hook.Forwarded);
        if (expected)
        {
            Assert.AreEqual("command", harness.Commands[0]);
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("dock-focus")]
    [DataRow("extension-command")]
    public void CallbackPreservesCommandId(string commandId)
    {
        using var harness = new Harness();
        harness.Listener.SetHotkeyAction(true, false, false, true, 0x20, commandId);
        harness.Hook.SetModifiers(win: true, alt: true);

        Assert.AreEqual(1, harness.Hook.Key(0x20));
        CollectionAssert.AreEqual(new[] { commandId }, harness.Commands);
    }

    [TestMethod]
    public void RepeatedKeyDownInvokesShortcutAgain()
    {
        using var harness = new Harness();
        harness.Listener.SetHotkeyAction(true, false, false, true, 0x20, string.Empty);
        harness.Hook.SetModifiers(win: true, alt: true);

        Assert.AreEqual(1, harness.Hook.Key(0x20));
        Assert.AreEqual(1, harness.Hook.Key(0x20));
        Assert.AreEqual(2, harness.Commands.Count);
        Assert.AreEqual(2, harness.Hook.DummyKeys);
    }

    [TestMethod]
    public void DuplicateShortcutInvokesFirstRegisteredCommand()
    {
        using var harness = new Harness();
        harness.Listener.SetHotkeyAction(true, false, false, true, 0x20, "first");
        harness.Listener.SetHotkeyAction(true, false, false, true, 0x20, "second");
        harness.Hook.SetModifiers(win: true, alt: true);

        Assert.AreEqual(1, harness.Hook.Key(0x20));
        Assert.AreEqual(1, harness.Commands.Count);
        Assert.AreEqual("first", harness.Commands[0]);
    }

    [TestMethod]
    public void ClearingHotkeysRemovesAllCommandsAndAllowsNewRegistration()
    {
        using var harness = new Harness();
        harness.Listener.SetHotkeyAction(true, false, false, true, 0x20, "first");
        harness.Listener.SetHotkeyAction(false, true, false, false, 0x4B, "second");
        harness.Listener.ClearHotkeys();

        harness.Hook.SetModifiers(win: true, alt: true);
        Assert.AreEqual(42, harness.Hook.Key(0x20));
        harness.Hook.SetModifiers(ctrl: true);
        Assert.AreEqual(42, harness.Hook.Key(0x4B));
        Assert.AreEqual(0, harness.Commands.Count);

        harness.Listener.SetHotkeyAction(true, false, false, true, 0x20, "replacement");
        harness.Hook.SetModifiers(win: true, alt: true);
        Assert.AreEqual(1, harness.Hook.Key(0x20));
        Assert.AreEqual(1, harness.Commands.Count);
        Assert.AreEqual("replacement", harness.Commands[0]);
    }

    [TestMethod]
    public void EmptyShortcutDoesNotInvokeCommand()
    {
        using var harness = new Harness();
        harness.Listener.SetHotkeyAction(false, false, false, false, 0, "empty");

        Assert.AreEqual(42, harness.Hook.Key(0));
        Assert.AreEqual(0, harness.Commands.Count);
    }

    [TestMethod]
    public void CommandCanReplaceHotkeysDuringCallback()
    {
        List<string> commands = [];
        Harness? harness = null;
        using (harness = new Harness(commandId =>
        {
            commands.Add(commandId);
            harness!.Listener.ClearHotkeys();
            harness.Listener.SetHotkeyAction(false, true, false, false, 0x4C, "replacement");
        }))
        {
            harness.Listener.SetHotkeyAction(false, true, false, false, 0x4B, "original");
            harness.Hook.SetModifiers(ctrl: true);

            Assert.AreEqual(1, harness.Hook.Key(0x4B));
            Assert.AreEqual(42, harness.Hook.Key(0x4B));
            Assert.AreEqual(1, harness.Hook.Key(0x4C));
            Assert.AreEqual(2, commands.Count);
            Assert.AreEqual("original", commands[0]);
            Assert.AreEqual("replacement", commands[1]);
        }
    }

    [TestMethod]
    [DataRow(-1, 0x0100)]
    [DataRow(0, 0x0101)]
    [DataRow(0, 0x0105)]
    public void NegativeCodesAndKeyUpAreForwardedWithoutReadingModifiers(int code, int message)
    {
        using var harness = new Harness();
        harness.Listener.SetHotkeyAction(true, false, false, false, 0x20, string.Empty);
        harness.Hook.SetModifiers(win: true);

        Assert.AreEqual(42, harness.Hook.Key(0x20, code, (uint)message));
        Assert.AreEqual(0, harness.Hook.ModifierReads);
        Assert.AreEqual(0, harness.Hook.DummyKeys);
        Assert.AreEqual(0, harness.Commands.Count);
    }

    [TestMethod]
    [DataRow(false, false, false, true)]
    [DataRow(false, false, true, true)]
    [DataRow(false, true, false, true)]
    [DataRow(true, false, false, false)]
    public void EveryMatchedShortcutInjectsDummyKeyUp(bool win, bool ctrl, bool shift, bool alt)
    {
        using var harness = new Harness();
        harness.Listener.SetHotkeyAction(win, ctrl, shift, alt, 0x4B, string.Empty);
        harness.Hook.SetModifiers(win, ctrl, shift, alt);

        Assert.AreEqual(1, harness.Hook.Key(0x4B, message: 0x0104));
        Assert.AreEqual(1, harness.Hook.DummyKeys);
    }

    [TestMethod]
    public void DummyKeyUpFollowsCommandExecution()
    {
        var dummyKeysAtCommand = -1;
        Harness? harness = null;
        using (harness = new Harness(_ => dummyKeysAtCommand = harness!.Hook.DummyKeys))
        {
            harness.Listener.SetHotkeyAction(false, true, false, false, 0x4B, string.Empty);
            harness.Hook.SetModifiers(ctrl: true);

            Assert.AreEqual(1, harness.Hook.Key(0x4B));
            Assert.AreEqual(0, dummyKeysAtCommand);
            Assert.AreEqual(1, harness.Hook.DummyKeys);
        }
    }

    [TestMethod]
    public async Task CommandExceptionIsReportedWithoutLeakingMatchedKeyOrSkippingDummyKeyUp()
    {
        var failure = new InvalidOperationException("Command failed");
        using var harness = new Harness(_ => throw failure);
        harness.Listener.SetHotkeyAction(true, false, false, false, 0x20, string.Empty);
        harness.Hook.SetModifiers(win: true);

        Assert.AreEqual(1, harness.Hook.Key(0x20));
        Assert.AreEqual(0, harness.Hook.Forwarded);
        Assert.AreEqual(1, harness.Hook.DummyKeys);
        Assert.AreSame(failure, await harness.ReportedError.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public async Task ErrorReportingDoesNotBlockHookCallback()
    {
        using var releaseReporter = new ManualResetEventSlim();
        var reporterStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reporterFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = new Harness(
            _ => throw new InvalidOperationException("Command failed"),
            _ =>
            {
                reporterStarted.SetResult();
                releaseReporter.Wait();
                reporterFinished.SetResult();
            });
        harness.Listener.SetHotkeyAction(false, true, false, false, 0x4B, string.Empty);
        harness.Hook.SetModifiers(ctrl: true);

        try
        {
            var callback = Task.Run(() => harness.Hook.Key(0x4B));
            await reporterStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(1, await callback.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(0, harness.Hook.Forwarded);
            Assert.AreEqual(1, harness.Hook.DummyKeys);
        }
        finally
        {
            releaseReporter.Set();
            await reporterFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    public async Task InputInjectionExceptionIsReportedWithoutLeakingMatchedKey()
    {
        using var harness = new Harness();
        harness.Listener.SetHotkeyAction(false, true, false, true, 0x4B, string.Empty);
        harness.Hook.SetModifiers(ctrl: true, alt: true);
        harness.Hook.ThrowOnDummyKey = true;

        Assert.AreEqual(1, harness.Hook.Key(0x4B));
        Assert.AreEqual(1, harness.Commands.Count);
        Assert.AreEqual(0, harness.Hook.Forwarded);
        Assert.IsInstanceOfType<InvalidOperationException>(await harness.ReportedError.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public void InstallationFailureReturnsErrorToCallerAndCanBeRetried()
    {
        var failure = new Win32Exception(5);
        var hook = new FakeHook { InstallError = failure };
        using var listener = new GlobalKeyboardListener(_ => { }, _ => { }, hook);

        Assert.IsFalse(listener.Start(out var error));
        Assert.AreSame(failure, error);
        Assert.AreEqual(0, hook.UninstallCount);

        hook.InstallError = null;
        Assert.IsTrue(listener.Start(out error));
        Assert.IsNull(error);
        Assert.AreEqual(2, hook.InstallAttempts);
    }

    [TestMethod]
    public void StartIsIdempotentAndStopAllowsRestart()
    {
        using var harness = new Harness();
        Assert.IsTrue(harness.Listener.Start(out var error));
        Assert.IsNull(error);
        Assert.AreEqual(1, harness.Hook.InstallAttempts);

        harness.Listener.Stop();
        harness.Listener.Stop();
        Assert.AreEqual(1, harness.Hook.UninstallCount);
        Assert.IsTrue(harness.Listener.Start(out error));
        Assert.IsNull(error);
        Assert.AreEqual(2, harness.Hook.InstallAttempts);
    }

    [TestMethod]
    public void DisposedListenerDoesNotInvokeCommandsOrRestart()
    {
        using var harness = new Harness();
        harness.Listener.SetHotkeyAction(false, true, false, false, 0x4B, "command");
        harness.Hook.SetModifiers(ctrl: true);
        harness.Listener.Dispose();
        harness.Listener.Dispose();

        Assert.AreEqual(42, harness.Hook.Key(0x4B));
        Assert.AreEqual(0, harness.Commands.Count);
        Assert.AreEqual(1, harness.Hook.UninstallCount);
        Assert.ThrowsExactly<ObjectDisposedException>(() => harness.Listener.Start(out _));
        Assert.ThrowsExactly<ObjectDisposedException>(() => harness.Listener.SetHotkeyAction(false, true, false, false, 0x4B, "command"));
    }

    private sealed partial class Harness : IDisposable
    {
        public Harness(Action<string>? processCommand = null, Action<Exception>? reportError = null)
        {
            Listener = new GlobalKeyboardListener(processCommand ?? Commands.Add, reportError ?? (error => ReportedError.TrySetResult(error)), Hook);
            Assert.IsTrue(Listener.Start(out var error), error?.ToString());
        }

        public FakeHook Hook { get; } = new();

        public List<string> Commands { get; } = [];

        public TaskCompletionSource<Exception> ReportedError { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public GlobalKeyboardListener Listener { get; }

        public void Dispose() => Listener.Dispose();
    }

    private sealed partial class FakeHook : IGlobalKeyboardHook
    {
        private readonly HashSet<VIRTUAL_KEY> _modifiers = [];
        private HOOKPROC _callback = null!;

        public Exception? InstallError { get; set; }

        public int InstallAttempts { get; private set; }

        public int UninstallCount { get; private set; }

        public int ModifierReads { get; private set; }

        public int DummyKeys { get; private set; }

        public int Forwarded { get; private set; }

        public bool ThrowOnDummyKey { get; set; }

        public IDisposable Install(HOOKPROC callback)
        {
            InstallAttempts++;
            if (InstallError is not null)
            {
                throw InstallError;
            }

            _callback = callback;
            return new Registration(() => UninstallCount++);
        }

        public void SetModifiers(bool win = false, bool ctrl = false, bool shift = false, bool alt = false)
        {
            _modifiers.Clear();
            if (win)
            {
                _modifiers.Add(VIRTUAL_KEY.VK_LWIN);
            }

            if (ctrl)
            {
                _modifiers.Add(VIRTUAL_KEY.VK_CONTROL);
            }

            if (shift)
            {
                _modifiers.Add(VIRTUAL_KEY.VK_SHIFT);
            }

            if (alt)
            {
                _modifiers.Add(VIRTUAL_KEY.VK_MENU);
            }
        }

        public bool IsKeyDown(VIRTUAL_KEY key)
        {
            ModifierReads++;
            return _modifiers.Contains(key);
        }

        public void SendDummyKeyUp()
        {
            DummyKeys++;
            if (ThrowOnDummyKey)
            {
                throw new InvalidOperationException("Input injection failed");
            }
        }

        public LRESULT CallNext(int code, WPARAM wParam, LPARAM lParam)
        {
            Forwarded++;
            return (LRESULT)42;
        }

        public unsafe int Key(uint key, int code = 0, uint message = 0x0100)
        {
            KBDLLHOOKSTRUCT data = new() { vkCode = key };
            return (int)_callback(code, (WPARAM)message, (LPARAM)(nint)(&data)).Value;
        }

        private sealed partial class Registration(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }
}
