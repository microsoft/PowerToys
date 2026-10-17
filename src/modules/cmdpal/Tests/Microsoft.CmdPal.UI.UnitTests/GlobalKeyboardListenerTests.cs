// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Settings;
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
        harness.Start(Shortcut(false, true, false, true, 0x4B, "command"));
        harness.Hook.SetModifiers(win, ctrl, shift, alt);

        Assert.AreEqual(expected ? 1 : 42, harness.Hook.Key((uint)key));
        Assert.AreEqual(0, harness.Commands.Count);
        harness.Drain();
        Assert.AreEqual(expected ? 1 : 0, harness.Commands.Count);
        Assert.AreEqual(expected ? 0 : 1, harness.Hook.Forwarded);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("dock-focus")]
    [DataRow("extension-command")]
    public void CallbackPreservesCommandIdAndPressTimestamp(string commandId)
    {
        using var harness = new Harness();
        harness.Start(Shortcut(true, false, false, false, 0x20, commandId));
        harness.Hook.SetModifiers(win: true);
        harness.Timestamp = 123;

        Assert.AreEqual(1, harness.Hook.Key(0x20));
        harness.Timestamp = 456;
        harness.Drain();

        Assert.AreEqual(1, harness.Commands.Count);
        Assert.AreEqual((commandId, 123L), harness.Commands[0]);
    }

    [TestMethod]
    [DataRow(-1, 0x0100)]
    [DataRow(0, 0x0101)]
    [DataRow(0, 0x0105)]
    public void CallbackForwardsNegativeCodesAndKeyUpWithoutReadingModifiers(int code, int message)
    {
        using var harness = new Harness();
        harness.Start(Shortcut(true, false, false, false, 0x20));
        harness.Hook.SetModifiers(win: true);

        Assert.AreEqual(42, harness.Hook.Key(0x20, code, (uint)message));
        Assert.AreEqual(0, harness.Hook.ModifierReads);
        Assert.IsTrue(harness.UiWork.IsEmpty);
    }

    [TestMethod]
    public void UnregisteredAndOutOfRangeKeysDoNotReadModifiers()
    {
        using var harness = new Harness();
        harness.Start(Shortcut(true, false, false, false, 0x20));
        harness.Hook.SetModifiers(win: true);

        Assert.AreEqual(42, harness.Hook.Key(0x41));
        Assert.AreEqual(42, harness.Hook.Key(0x120));
        Assert.AreEqual(0, harness.Hook.ModifierReads);
        Assert.IsTrue(harness.UiWork.IsEmpty);
    }

    [TestMethod]
    public void UnregisteredKeyDoesNotAllocateInCallback()
    {
        using var harness = new Harness();
        harness.Start(Shortcut(true, false, false, false, 0x20));
        harness.Hook.Key(0x41);
        harness.Hook.Key(0x41);

        Assert.AreEqual(0L, harness.Hook.CallbackAllocatedBytes);
    }

    [TestMethod]
    [DataRow(false, false, false, true)]
    [DataRow(false, false, true, true)]
    [DataRow(false, true, false, true)]
    [DataRow(true, false, false, false)]
    public void EveryMatchedShortcutInjectsDummyKeyUp(bool win, bool ctrl, bool shift, bool alt)
    {
        using var harness = new Harness();
        harness.Start(Shortcut(win, ctrl, shift, alt, 0x4B));
        harness.Hook.SetModifiers(win, ctrl, shift, alt);

        Assert.AreEqual(1, harness.Hook.Key(0x4B, message: 0x0104));
        Assert.AreEqual(1, harness.Hook.DummyKeys);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CommandExceptionIsReportedWithoutLeakingMatchedKey(bool failReporting)
    {
        var failure = new InvalidOperationException("Command failed");
        using var harness = new Harness((_, _) => throw failure) { ThrowOnReportError = failReporting };
        harness.Start(Shortcut(true, false, false, false, 0x20));
        harness.Hook.SetModifiers(win: true);

        Assert.AreEqual(1, harness.Hook.Key(0x20));
#if DEBUG
        if (failReporting)
        {
            Assert.ThrowsExactly<IOException>(harness.Drain);
        }
        else
#endif
        {
            harness.Drain();
        }

        Assert.AreEqual(0, harness.Hook.Forwarded);
        Assert.AreEqual(1, harness.Hook.DummyKeys);
        Assert.AreSame(failure, await harness.ReportedError.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ErrorReportingDoesNotBlockHookCallback(bool renewalFailure)
    {
        using var releaseReporter = new ManualResetEventSlim();
        var reporterStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reporterFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = new Harness(reportError: _ =>
        {
            reporterStarted.SetResult();
            releaseReporter.Wait();
            reporterFinished.SetResult();
        });
        harness.Start(Shortcut(false, true, false, false, 0x4B));
        harness.Hook.SetModifiers(ctrl: true);
        if (renewalFailure)
        {
            harness.Hook.InstallError = new Win32Exception(5);
            harness.Hook.RenewHook();
        }
        else
        {
            harness.ThrowOnEnqueue = true;
        }

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
        harness.Start(Shortcut(false, true, false, true, 0x4B));
        harness.Hook.SetModifiers(ctrl: true, alt: true);
        harness.Hook.ThrowOnDummyKey = true;

        Assert.AreEqual(1, harness.Hook.Key(0x4B));
        Assert.AreEqual(0, harness.Hook.Forwarded);
        Assert.IsInstanceOfType<InvalidOperationException>(await harness.ReportedError.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DispatchFailureStillConsumesShortcutAndSuppressesStartMenu(bool throws)
    {
        using var harness = new Harness();
        harness.Start(Shortcut(true, false, false, false, 0x20));
        harness.Hook.SetModifiers(win: true);
        harness.RejectEnqueue = !throws;
        harness.ThrowOnEnqueue = throws;

        Assert.AreEqual(1, harness.Hook.Key(0x20));
        Assert.AreEqual(0, harness.Hook.Forwarded);
        Assert.AreEqual(1, harness.Hook.DummyKeys);
        Assert.IsTrue(harness.UiWork.IsEmpty);
        if (throws)
        {
            Assert.IsInstanceOfType<InvalidOperationException>(await harness.ReportedError.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }

    [TestMethod]
    public void HookThreadKeepsProcessingWhileUiWorkIsPending()
    {
        using var harness = new Harness();
        harness.Start(Shortcut(false, true, false, false, 0x4B));
        harness.Hook.SetModifiers(ctrl: true);

        Assert.AreEqual(1, harness.Hook.Key(0x4B));
        Assert.AreEqual(42, harness.Hook.Key(0x41));
        Assert.AreEqual(1, harness.Hook.Key(0x4B));
        Assert.AreEqual(0, harness.Commands.Count);
        Assert.AreNotEqual(Environment.CurrentManagedThreadId, harness.Hook.InstallThread);
        Assert.AreEqual(harness.Hook.InstallThread, harness.Hook.CallbackThread);
        Assert.AreEqual(ThreadPriority.AboveNormal, harness.Hook.Priority);

        harness.Drain();
        Assert.AreEqual(2, harness.Commands.Count);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void QueuedPressesUseInputTimingForBreakthrough(bool rapid)
    {
        var detector = new ShortcutBreakthroughDetector();
        List<bool> triggered = [];
        using var harness = new Harness((_, timestamp) => triggered.Add(detector.RegisterPress(timestamp)));
        harness.Start(Shortcut(false, true, false, false, 0x4B));
        harness.Hook.SetModifiers(ctrl: true);
        var interval = rapid ? Stopwatch.Frequency / 2 : 3 * Stopwatch.Frequency;

        for (var i = 0; i < 3; i++)
        {
            harness.Timestamp = i * interval;
            Assert.AreEqual(1, harness.Hook.Key(0x4B));
        }

        harness.Drain();
        Assert.AreEqual(3, triggered.Count);
        Assert.IsFalse(triggered[0]);
        Assert.IsFalse(triggered[1]);
        Assert.AreEqual(rapid, triggered[2]);
    }

    [TestMethod]
    public void ReplacingHotkeysInvalidatesQueuedCommandsAndKeepsFirstDuplicate()
    {
        using var harness = new Harness();
        harness.Start(Shortcut(false, true, false, false, 0x4B, "old"));
        harness.Hook.SetModifiers(ctrl: true);
        Assert.AreEqual(1, harness.Hook.Key(0x4B));

        harness.Listener.SetHotkeys([
            Shortcut(false, true, false, false, 0x4C, "first"),
            Shortcut(false, true, false, false, 0x4C, "second"),
        ]);

        Assert.AreEqual(42, harness.Hook.Key(0x4B));
        Assert.AreEqual(1, harness.Hook.Key(0x4C));
        harness.Drain();
        Assert.AreEqual(1, harness.Commands.Count);
        Assert.AreEqual("first", harness.Commands[0].CommandId);
    }

    [TestMethod]
    public void EquivalentRegistrationPreservesQueuedPress()
    {
        using var harness = new Harness();
        harness.Start(Shortcut(false, true, false, false, 0x4B, "command"));
        harness.Hook.SetModifiers(ctrl: true);
        Assert.AreEqual(1, harness.Hook.Key(0x4B));

        harness.Listener.SetHotkeys([Shortcut(false, true, false, false, 0x4B, "command")]);
        harness.Drain();

        Assert.AreEqual(1, harness.Commands.Count);
        Assert.AreEqual("command", harness.Commands[0].CommandId);
    }

    [TestMethod]
    public void RenewalReplacesHookOnOwningThreadWithoutInvalidatingQueuedPresses()
    {
        using var harness = new Harness();
        harness.Start(Shortcut(false, true, false, false, 0x4B));
        harness.Hook.SetModifiers(ctrl: true);
        Assert.AreEqual(1, harness.Hook.Key(0x4B));

        harness.Hook.RenewHook();
        Assert.AreEqual(1, harness.Hook.Key(0x4B));
        harness.Drain();

        Assert.AreEqual(2, harness.Hook.InstallCount);
        Assert.AreEqual(1, harness.Hook.UninstallCount);
        Assert.AreEqual(harness.Hook.InstallThread, harness.Hook.UninstallThread);
        Assert.AreEqual(2, harness.Commands.Count);
    }

    [TestMethod]
    public async Task FailedRenewalsKeepHookAndQueuedPressesUntilSuccessfulRetry()
    {
        ConcurrentQueue<Exception> errors = new();
        using var reported = new SemaphoreSlim(0);
        using var harness = new Harness(reportError: error =>
        {
            errors.Enqueue(error);
            reported.Release();
        });
        harness.Start(Shortcut(false, true, false, false, 0x4B));
        harness.Hook.SetModifiers(ctrl: true);
        Assert.AreEqual(1, harness.Hook.Key(0x4B));
        var error = new Win32Exception(5);
        harness.Hook.InstallError = error;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            harness.Hook.RenewHook();
            Assert.AreEqual(1, harness.Hook.Key(0x4B));
            Assert.IsTrue(await reported.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(errors.TryDequeue(out var reportedError));
            Assert.AreSame(error, reportedError.InnerException);
        }

        harness.Drain();
        Assert.AreEqual(0, harness.Hook.UninstallCount);
        Assert.AreEqual(0, harness.Errors.Count);
        Assert.AreEqual(3, harness.Commands.Count);

        harness.Hook.InstallError = null;
        harness.Hook.RenewHook();
        Assert.AreEqual(1, harness.Hook.Key(0x4B));
        harness.Drain();

        Assert.AreEqual(2, harness.Hook.InstallCount);
        Assert.AreEqual(1, harness.Hook.UninstallCount);
        Assert.AreEqual(0, harness.Errors.Count);
        Assert.AreEqual(4, harness.Commands.Count);
    }

    [TestMethod]
    public void ClearingShortcutsPassesThroughKeysAndCancelsQueuedCommands()
    {
        using var harness = new Harness();
        harness.Start(Shortcut(false, true, false, false, 0x4B));
        harness.Hook.SetModifiers(ctrl: true);
        Assert.AreEqual(1, harness.Hook.Key(0x4B));

        harness.Listener.SetHotkeys([]);

        Assert.AreEqual(42, harness.Hook.Key(0x4B));
        harness.Drain();
        Assert.AreEqual(0, harness.Commands.Count);
        Assert.AreEqual(1, harness.Hook.InstallCount);
    }

    [TestMethod]
    public void StartIsIdempotentAndStopInvalidatesQueuedWorkBeforeRestart()
    {
        using var harness = new Harness();
        harness.Start(Shortcut(false, true, false, false, 0x4B));
        Assert.IsTrue(harness.Listener.Start(out _));
        Assert.AreEqual(1, harness.Hook.InstallCount);
        harness.Hook.SetModifiers(ctrl: true);
        Assert.AreEqual(1, harness.Hook.Key(0x4B));

        harness.Listener.Stop();
        Assert.AreEqual(1, harness.Hook.UninstallCount);
        Assert.AreEqual(harness.Hook.InstallThread, harness.Hook.UninstallThread);
        Assert.IsTrue(harness.Listener.Start(out _));
        harness.Drain();
        Assert.AreEqual(0, harness.Commands.Count);

        Assert.AreEqual(1, harness.Hook.Key(0x4B));
        harness.Drain();
        Assert.AreEqual(1, harness.Commands.Count);
    }

    [TestMethod]
    public void StartupFailureIsReturnedAndCanBeRetried()
    {
        using var harness = new Harness();
        var failure = new Win32Exception(5);
        harness.Hook.InstallError = failure;

        Assert.IsFalse(harness.Listener.Start(out var error));
        Assert.AreSame(failure, error);
        Assert.IsTrue(harness.UiWork.IsEmpty);

        harness.Hook.InstallError = null;
        Assert.IsTrue(harness.Listener.Start(out error));
        Assert.IsNull(error);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InterruptedShutdownCompletesCleanupAndRestoresInterrupt(bool dispose)
    {
        using var harness = new Harness();
        using var unhookStarted = new ManualResetEventSlim();
        using var finishUnhook = new ManualResetEventSlim();
        using var stopped = new ManualResetEventSlim();
        var clears = 0;
        var unhooksAtClear = 0;
        List<Exception> serviceErrors = [];
        List<bool> notifications = [];
        using var service = new GlobalHotkeyService(
            harness.Listener,
            () =>
            {
                clears++;
                unhooksAtClear = harness.Hook.UninstallCount;
            },
            _ => true,
            (_, error) => serviceErrors.Add(error),
            notifications.Add);
        Assert.IsTrue(service.Configure(true, [Shortcut(false, true, false, false, 0x4B)]));
        clears = 0;
        harness.Hook.SetModifiers(ctrl: true);
        Assert.AreEqual(1, harness.Hook.Key(0x4B));
        harness.Hook.BeforeUninstall = () =>
        {
            unhookStarted.Set();
            finishUnhook.Wait();
        };
        Exception? escaped = null;
        var interruptRestored = false;
        var owner = new Thread(() =>
        {
            try
            {
                if (dispose)
                {
                    service.Dispose();
                }
                else
                {
                    service.Stop();
                }
            }
            catch (Exception ex)
            {
                escaped = ex;
            }
            finally
            {
                stopped.Set();
            }

            try
            {
                Thread.Sleep(0);
            }
            catch (ThreadInterruptedException)
            {
                interruptRestored = true;
            }
        });
        owner.Start();
        try
        {
            Assert.IsTrue(unhookStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(SpinWait.SpinUntil(() => (owner.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(5)));
            owner.Interrupt();
            Assert.IsFalse(stopped.IsSet);
        }
        finally
        {
            finishUnhook.Set();
            Assert.IsTrue(owner.Join(TimeSpan.FromSeconds(5)));
        }

        Assert.IsNull(escaped);
        Assert.IsTrue(interruptRestored, "Cleanup must preserve the interrupt for the caller's next wait.");
        Assert.IsFalse(harness.ReportedError.Task.IsCompleted, "Recovered interrupts must not be reported as errors.");
        Assert.AreEqual(1, clears);
        Assert.AreEqual(1, unhooksAtClear, "The hook must finish teardown before standard hotkeys are cleared.");
        Assert.AreEqual(0, serviceErrors.Count);
        Assert.AreEqual(0, notifications.Count);
        Assert.AreEqual(1, harness.Hook.UninstallCount);
        harness.Drain();
        Assert.AreEqual(0, harness.Commands.Count);
        harness.Hook.BeforeUninstall = null;
        if (dispose)
        {
            Assert.ThrowsExactly<ObjectDisposedException>(() => harness.Listener.Start(out _));
        }
        else
        {
            Assert.IsTrue(service.Configure(true, [Shortcut(false, true, false, false, 0x4B)]));
            Assert.AreEqual(2, harness.Hook.InstallCount);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InterruptedStartupPreservesHookResultAndRestoresInterrupt(bool failInstall)
    {
        using var harness = new Harness();
        using var installStarted = new ManualResetEventSlim();
        using var finishInstall = new ManualResetEventSlim();
        using var startupCompleted = new ManualResetEventSlim();
        var failure = failInstall ? new Win32Exception(5) : null;
        harness.Hook.InstallError = failure;
        harness.Listener.SetHotkeys([Shortcut(false, true, false, false, 0x4B)]);
        harness.Hook.BeforeInstall = () =>
        {
            installStarted.Set();
            finishInstall.Wait();
        };
        Exception? escaped = null;
        Exception? startupError = null;
        var started = false;
        var interruptRestored = false;
        var owner = new Thread(() =>
        {
            try
            {
                started = harness.Listener.Start(out startupError);
            }
            catch (Exception ex)
            {
                escaped = ex;
            }
            finally
            {
                startupCompleted.Set();
            }

            try
            {
                Thread.Sleep(0);
            }
            catch (ThreadInterruptedException)
            {
                interruptRestored = true;
            }
        });
        owner.Start();
        try
        {
            Assert.IsTrue(installStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(SpinWait.SpinUntil(() => (owner.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(5)));
            owner.Interrupt();
            Assert.IsFalse(startupCompleted.IsSet);
        }
        finally
        {
            finishInstall.Set();
            Assert.IsTrue(owner.Join(TimeSpan.FromSeconds(5)));
        }

        Assert.IsNull(escaped);
        Assert.IsTrue(interruptRestored, "Startup must preserve the interrupt for the caller's next wait.");
        Assert.IsFalse(harness.ReportedError.Task.IsCompleted, "Recovered interrupts must not be reported as errors.");
        Assert.AreEqual(!failInstall, started);
        Assert.AreSame(failure, startupError);
        Assert.AreEqual(failInstall ? 0 : 1, harness.Hook.InstallCount);
        Assert.AreEqual(0, harness.Hook.UninstallCount);
        Assert.IsTrue(harness.UiWork.IsEmpty);
        harness.Hook.BeforeInstall = null;
        harness.Hook.InstallError = null;
        Assert.IsTrue(harness.Listener.Start(out _));
        Assert.AreEqual(1, harness.Hook.InstallCount);
        harness.Hook.SetModifiers(ctrl: true);
        Assert.AreEqual(1, harness.Hook.Key(0x4B));
        harness.Drain();
        Assert.AreEqual(1, harness.Commands.Count);
    }

    [TestMethod]
    public void MessageLoopFailureUnhooksAndReportsToUi()
    {
        using var harness = new Harness();
        harness.Start(Shortcut(false, true, false, false, 0x4B));
        harness.Hook.FailMessageLoop();
        Assert.IsTrue(SpinWait.SpinUntil(() => !harness.UiWork.IsEmpty, TimeSpan.FromSeconds(5)));

        harness.Drain();
        Assert.AreEqual(1, harness.Errors.Count);
        Assert.AreEqual(1, harness.Hook.UninstallCount);
    }

    [TestMethod]
    [DataRow(false)]
#if !DEBUG
    [DataRow(true)]
#endif
    public void MessageLoopFailureCanStopWhenDispatcherThrows(bool failReporting)
    {
        using var harness = new Harness { ThrowOnReportError = failReporting };
        harness.Start(Shortcut(false, true, false, false, 0x4B));
        harness.ThrowOnEnqueue = true;
        harness.Hook.FailMessageLoop();
        Assert.IsTrue(harness.Hook.Uninstalled.Wait(TimeSpan.FromSeconds(5)));

        harness.Listener.Stop();
        Assert.IsTrue(harness.UiWork.IsEmpty);
        Assert.AreEqual(1, harness.Hook.UninstallCount);
        Assert.IsTrue(harness.ReportedError.Task.IsCompletedSuccessfully);
        Assert.IsInstanceOfType<InvalidOperationException>(harness.ReportedError.Task.Result);
    }

    [TestMethod]
    public void NativeHookRejectsZeroRenewalInterval()
    {
        var error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new GlobalKeyboardHook(0));

        Assert.AreEqual("renewalIntervalMilliseconds", error.ParamName);
    }

    [TestMethod]
    public void NativeMessageLoopCanStopWithoutKeyboardInput()
    {
        using var stop = new ManualResetEvent(false);
        var hook = new GlobalKeyboardHook();
        var loop = Task.Run(() => hook.RunMessageLoop(stop, () => Assert.Fail("Unexpected renewal")));
        stop.Set();

        Assert.IsTrue(loop.Wait(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public void NativeMessageLoopRenewsWithoutKeyboardInput()
    {
        using var stop = new ManualResetEvent(false);
        var hook = new GlobalKeyboardHook(10);
        var renewals = 0;
        var loop = Task.Run(() => hook.RunMessageLoop(stop, () =>
        {
            renewals++;
            stop.Set();
        }));

        Assert.IsTrue(loop.Wait(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(1, renewals);
    }

    [TestMethod]
    public void DisposeCancelsQueuedCommandsAndPreventsRestart()
    {
        using var harness = new Harness();
        harness.Start(Shortcut(false, true, false, false, 0x4B));
        harness.Hook.SetModifiers(ctrl: true);
        Assert.AreEqual(1, harness.Hook.Key(0x4B));

        harness.Listener.Dispose();
        harness.Listener.Dispose();
        harness.Drain();

        Assert.AreEqual(0, harness.Commands.Count);
        Assert.AreEqual(1, harness.Hook.UninstallCount);
        Assert.ThrowsExactly<ObjectDisposedException>(() => harness.Listener.Start(out _));
        Assert.ThrowsExactly<ObjectDisposedException>(() => harness.Listener.SetHotkeys([]));
    }

    private static TopLevelHotkey Shortcut(bool win, bool ctrl, bool shift, bool alt, int key, string commandId = "") =>
        new(new HotkeySettings(win, ctrl, alt, shift, key), commandId);

    private sealed partial class Harness : IDisposable
    {
        public Harness(Action<string, long>? processCommand = null, Action<Exception>? reportError = null)
        {
            Listener = new GlobalKeyboardListener(
                action =>
                {
                    if (ThrowOnEnqueue)
                    {
                        throw new InvalidOperationException("Dispatcher unavailable");
                    }

                    if (RejectEnqueue)
                    {
                        return false;
                    }

                    UiWork.Enqueue(action);
                    return true;
                },
                processCommand ?? ((commandId, timestamp) => Commands.Add((commandId, timestamp))),
                Errors.Add,
                error =>
                {
                    ReportedError.TrySetResult(error);
                    reportError?.Invoke(error);
                    if (ThrowOnReportError)
                    {
                        throw new IOException("Error reporter failed");
                    }
                },
                Hook,
                () => Timestamp);
        }

        public FakeHook Hook { get; } = new();

        public ConcurrentQueue<Action> UiWork { get; } = new();

        public List<(string CommandId, long Timestamp)> Commands { get; } = [];

        public List<Exception> Errors { get; } = [];

        public TaskCompletionSource<Exception> ReportedError { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public GlobalKeyboardListener Listener { get; }

        public long Timestamp { get; set; }

        public bool RejectEnqueue { get; set; }

        public bool ThrowOnEnqueue { get; set; }

        public bool ThrowOnReportError { get; set; }

        public void Start(params TopLevelHotkey[] hotkeys)
        {
            Listener.SetHotkeys(hotkeys);
            Assert.IsTrue(Listener.Start(out var error), error?.ToString());
        }

        public void Drain()
        {
            while (UiWork.TryDequeue(out var action))
            {
                action();
            }
        }

        public void Dispose()
        {
            Listener.Dispose();
            Hook.Dispose();
        }
    }

    private sealed partial class FakeHook : IGlobalKeyboardHook, IDisposable
    {
        private readonly AutoResetEvent _workReady = new(false);
        private readonly ConcurrentQueue<Action> _work = new();
        private readonly HashSet<VIRTUAL_KEY> _modifiers = [];
        private HOOKPROC _callback = null!;
        private Action _renewHook = null!;

        public Exception? InstallError { get; set; }

        public Action? BeforeInstall { get; set; }

        public Action? BeforeUninstall { get; set; }

        public ManualResetEventSlim Uninstalled { get; } = new();

        public int InstallCount { get; private set; }

        public int UninstallCount { get; private set; }

        public int InstallThread { get; private set; }

        public int CallbackThread { get; private set; }

        public int UninstallThread { get; private set; }

        public ThreadPriority Priority { get; private set; }

        public int ModifierReads { get; private set; }

        public int DummyKeys { get; private set; }

        public bool ThrowOnDummyKey { get; set; }

        public int Forwarded { get; private set; }

        public long CallbackAllocatedBytes { get; private set; }

        public IDisposable Install(HOOKPROC callback)
        {
            BeforeInstall?.Invoke();
            if (InstallError is not null)
            {
                throw InstallError;
            }

            _callback = callback;
            InstallCount++;
            InstallThread = Environment.CurrentManagedThreadId;
            Priority = Thread.CurrentThread.Priority;
            return new Registration(() =>
            {
                BeforeUninstall?.Invoke();
                UninstallCount++;
                UninstallThread = Environment.CurrentManagedThreadId;
                Uninstalled.Set();
            });
        }

        public void RunMessageLoop(WaitHandle stop, Action renewHook)
        {
            _renewHook = renewHook;
            WaitHandle[] handles = [stop, _workReady];
            while (WaitHandle.WaitAny(handles) != 0)
            {
                while (_work.TryDequeue(out var action))
                {
                    action();
                }
            }
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
            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            _work.Enqueue(() =>
            {
                CallbackThread = Environment.CurrentManagedThreadId;
                KBDLLHOOKSTRUCT data = new() { vkCode = key };
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var result = _callback(code, (WPARAM)message, (LPARAM)(nint)(&data));
                CallbackAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                completion.SetResult((int)result.Value);
            });
            _workReady.Set();
            return completion.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }

        public void FailMessageLoop()
        {
            _work.Enqueue(() => throw new InvalidOperationException("Message loop failed"));
            _workReady.Set();
        }

        public void RenewHook()
        {
            _work.Enqueue(() => _renewHook());
            _workReady.Set();
        }

        public void Dispose()
        {
            _workReady.Dispose();
            Uninstalled.Dispose();
        }

        private sealed partial class Registration : IDisposable
        {
            private readonly Action _dispose;

            public Registration(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose() => _dispose();
        }
    }
}
