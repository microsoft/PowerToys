// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class ModifierReleaseTests
{
    private static readonly IntPtr Target = new(10);
    private static readonly IntPtr Other = new(11);

    [TestMethod]
    public void LosingFocusAfterARealMatchReleasesOnlyTheModifierWindowsReceived()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        File.WriteAllText(path, SnippetJson.Of("btw=By the way"));
        using var store = new SnippetStore(path);
        Assert.AreEqual(1, store.Load().Count);
        using var hook = new KeyboardHook(store)
        {
            CaptureForeground = static () => KeyboardHook.ForegroundCapture.Authorized,
            InPasswordBox = static () => false,
            UserInput = static () => { },
        };
        using var harness = new Harness(hook.Guard);
        harness.Key = key => hook.OnKeyEvent(key, (key.Flags & Native.LLKHF_UP) == 0 ? Native.WM_KEYDOWN : 0x0101, observePassed: false);
        bool wordMode = WordBoundary.Required;
        try
        {
            WordBoundary.Required = true;
            Assert.IsTrue(harness.Take(Vk(Native.VK_LSHIFT)));
            Assert.IsTrue(harness.Take(Vk(Native.VK_RSHIFT)));
            foreach (char ch in "btw")
            {
                Assert.IsTrue(harness.Take(FakeTextBox.UserKey(ch, up: false)));
            }

            Assert.IsFalse(harness.Take(FakeTextBox.UserKey(' ', up: false)));
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
            Assert.IsFalse(harness.Take(Vk(Native.VK_LSHIFT, up: true)));
            Assert.IsFalse(harness.Take(FakeTextBox.UserKey('x', up: false)));
            int hookThread = Environment.CurrentManagedThreadId;
            harness.Guard.ForegroundChanged(new IntPtr(work.TargetWindow.ToInt64() ^ 1));
            harness.Guard.Dispose();

            Native.INPUT[] sent = harness.Sent.Single();
            Assert.AreEqual(1, sent.Length, "Neither the swallowed space nor later text goes to the new window.");
            Assert.AreEqual((ushort)Native.VK_LSHIFT, sent[0].U.Ki.WVk);
            Assert.AreEqual(Native.KEYEVENTF_KEYUP, sent[0].U.Ki.DwFlags);
            Assert.AreNotEqual(hookThread, harness.SendThread, "SendInput is never called on the hook/foreground thread.");
            Assert.IsFalse(harness.IsDown(Native.VK_LSHIFT));
            Assert.IsTrue(harness.IsDown(Native.VK_RSHIFT), "A modifier still physically held is not blanket-released.");
        }
        finally
        {
            WordBoundary.Required = wordMode;
        }
    }

    [TestMethod]
    [DataRow("foreground")]
    [DataRow("release")]
    [DataRow("timeout")]
    [DataRow("abandon")]
    [DataRow("dispose")]
    public void EveryDiscardPathKeepsAnAlreadyPassedModifierPaired(string ending)
    {
        using var harness = new Harness();
        Assert.IsTrue(harness.Take(Vk(Native.VK_LSHIFT)));
        Assert.IsTrue(harness.Guard.HoldAfterMatch(Target, 1, FakeTextBox.UserKey('x', up: false)));
        Assert.IsFalse(harness.Take(FakeTextBox.UserKey('a', up: false)));
        Assert.IsFalse(harness.Take(Vk(Native.VK_LSHIFT, up: true)));
        harness.InFront = false;
        switch (ending)
        {
            case "foreground":
                harness.Guard.ForegroundChanged(Other);
                break;
            case "release":
                harness.Guard.ReleaseAfterMatch(1);
                break;
            case "timeout":
                harness.Now = InsertionGuard.TimeoutMs;
                harness.Guard.Expire();
                break;
            case "abandon":
                harness.Guard.Begin(1, Target);
                harness.Guard.Abandon();
                break;
        }

        harness.Guard.Dispose();
        Assert.IsFalse(harness.IsDown(Native.VK_LSHIFT));
        Assert.AreEqual("LShift-up", FakeTextBox.Describe(harness.Sent.Single()));
    }

    [TestMethod]
    [DataRow(Native.VK_LSHIFT, false)]
    [DataRow(Native.VK_RSHIFT, false)]
    [DataRow(Native.VK_LCONTROL, false)]
    [DataRow(Native.VK_RCONTROL, true)]
    [DataRow(Native.VK_LMENU, false)]
    [DataRow(Native.VK_RMENU, true)]
    [DataRow(Native.VK_LWIN, true)]
    [DataRow(Native.VK_RWIN, true)]
    public void CleanupPreservesTheSideAndExtendedFlagOfEachModifier(int vk, bool extended)
    {
        using var harness = new Harness();
        uint flags = extended ? Native.LLKHF_EXTENDED : 0;
        Assert.IsTrue(harness.Take(Vk(vk) with { Flags = flags }));
        Assert.IsTrue(harness.Guard.HoldAfterMatch(Target, 1, FakeTextBox.UserKey('x', up: false)));
        Assert.IsFalse(harness.Take(Vk(vk, up: true) with { Flags = flags | Native.LLKHF_UP }));

        harness.Guard.ForegroundChanged(Other);
        harness.Guard.Dispose();

        Native.KEYBDINPUT release = harness.Sent.Single().Single().U.Ki;
        Assert.AreEqual((ushort)vk, release.WVk);
        Assert.AreEqual(Native.KEYEVENTF_KEYUP | (extended ? Native.KEYEVENTF_EXTENDEDKEY : 0), release.DwFlags);
        Assert.IsFalse(harness.IsDown(vk));
    }

    [TestMethod]
    [DataRow("held")]
    [DataRow("lower")]
    [DataRow("suppressed")]
    [DataRow("remapped")]
    [DataRow("released")]
    [DataRow("not-down")]
    public void CleanupDoesNotInventADownThatWindowsNeverReceivedOrAlreadyReleased(string control)
    {
        using var harness = new Harness();
        if (control != "held")
        {
            Native.KBDLLHOOKSTRUCT down = Vk(Native.VK_LSHIFT);
            if (control == "lower")
            {
                down = down with { Flags = Native.LLKHF_LOWER_IL_INJECTED | 0x10 };
            }
            else if (control == "suppressed")
            {
                down = down with { DwExtraInfo = HostInputTags.KeyboardManagerSuppressed };
            }

            _ = harness.Take(down, downstreamAllows: control is not ("remapped" or "suppressed"));
        }

        Assert.IsTrue(harness.Guard.HoldAfterMatch(Target, 1, FakeTextBox.UserKey('x', up: false)));
        if (control == "held")
        {
            Assert.IsFalse(harness.Take(Vk(Native.VK_LSHIFT)));
        }

        Assert.IsFalse(harness.Take(Vk(Native.VK_LSHIFT, up: true)));
        if (control == "released")
        {
            Assert.IsTrue(harness.Take(Vk(Native.VK_LSHIFT, up: true) with { DwExtraInfo = KeyboardHook.InjectionSignature }));
        }
        else if (control == "not-down")
        {
            harness.Guard.ModifierIsDown = static _ => false;
        }

        harness.Guard.ForegroundChanged(Other);
        harness.Guard.Dispose();
        Assert.AreEqual(0, harness.Sent.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ANewPhysicalModifierEventInvalidatesCleanupAlreadyWaitingInSendInput(bool alsoRelease)
    {
        using var sending = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        using var harness = new Harness();
        harness.BeforeSend = _ =>
        {
            sending.Set();
            resume.Wait(10_000);
        };
        Assert.IsTrue(harness.Take(Vk(Native.VK_LSHIFT)));
        Assert.IsTrue(harness.Guard.HoldAfterMatch(Target, 1, FakeTextBox.UserKey('x', up: false)));
        Assert.IsFalse(harness.Take(Vk(Native.VK_LSHIFT, up: true)));
        harness.Guard.ForegroundChanged(Other);
        try
        {
            Assert.IsTrue(sending.Wait(10_000));
            Assert.IsTrue(harness.Take(Vk(Native.VK_LSHIFT)));
            if (alsoRelease)
            {
                Assert.IsTrue(harness.Take(Vk(Native.VK_LSHIFT, up: true)));
            }

            resume.Set();
            harness.Guard.Dispose();
            Assert.AreEqual(!alsoRelease, harness.IsDown(Native.VK_LSHIFT));
            Assert.AreEqual(alsoRelease ? 1 : 0, harness.Passed.Count(key => key.VkCode == Native.VK_LSHIFT && (key.Flags & Native.LLKHF_UP) != 0));
        }
        finally
        {
            resume.Set();
        }
    }

    [TestMethod]
    public void ReentrantInputInALaterHookAlsoInvalidatesCleanup()
    {
        using var harness = new Harness();
        Assert.IsTrue(harness.Take(Vk(Native.VK_LSHIFT)));
        Assert.IsTrue(harness.Guard.HoldAfterMatch(Target, 1, FakeTextBox.UserKey('x', up: false)));
        Assert.IsFalse(harness.Take(Vk(Native.VK_LSHIFT, up: true)));
        harness.Key = key =>
        {
            bool swallowed = harness.Guard.Swallows(key, observePassed: false);
            if (!swallowed && key.DwExtraInfo != IntPtr.Zero && (key.Flags & Native.LLKHF_UP) != 0)
            {
                // CallNextHookEx runs another hook, which causes a fresh physical press before returning zero.
                _ = harness.Take(Vk(Native.VK_LSHIFT));
            }

            return swallowed;
        };
        harness.Guard.ForegroundChanged(Other);
        harness.Guard.Dispose();
        Assert.IsTrue(harness.IsDown(Native.VK_LSHIFT));
        Assert.AreEqual(0, harness.Passed.Count(key => (key.Flags & Native.LLKHF_UP) != 0));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SameWindowReplayKeepsTheModifierContextOfHeldTyping(bool timeout)
    {
        using var harness = new Harness();
        Assert.IsTrue(harness.Take(Vk(Native.VK_LSHIFT)));
        Assert.IsTrue(harness.Guard.HoldAfterMatch(Target, 1, FakeTextBox.UserKey('x', up: false)));
        Assert.IsFalse(harness.Take(Vk(0x41)));
        Assert.IsFalse(harness.Take(Vk(0x41, up: true)));
        Assert.IsFalse(harness.Take(Vk(Native.VK_LSHIFT, up: true)));
        bool shiftedA = false;
        harness.OnPassed = key =>
        {
            if (key.VkCode == 0x41 && (key.Flags & Native.LLKHF_UP) == 0)
            {
                shiftedA = harness.IsDown(Native.VK_LSHIFT);
            }
        };
        if (timeout)
        {
            harness.Now = InsertionGuard.TimeoutMs;
            harness.Guard.Expire();
        }
        else
        {
            harness.Guard.ReleaseAfterMatch(1);
        }

        harness.Guard.Dispose();
        Assert.IsTrue(shiftedA, "An early release must not turn the held capital into lowercase.");
        Assert.IsFalse(harness.IsDown(Native.VK_LSHIFT));
        Assert.AreEqual("x-down A LShift-up", FakeTextBox.Describe(harness.Sent.Single()));
    }

    [TestMethod]
    public void AReleaseMovesWithANestedMatchDuringReplay()
    {
        using var harness = new Harness();
        Assert.IsTrue(harness.Take(Vk(Native.VK_LSHIFT)));
        byte id = harness.Guard.Begin(1, Target);
        Assert.IsFalse(harness.Take(FakeTextBox.Marker(id)));
        Assert.IsFalse(harness.Take(FakeTextBox.UserKey('x', up: false)));
        Assert.IsFalse(harness.Take(Vk(Native.VK_LSHIFT, up: true)));
        harness.OnPassed = key =>
        {
            if (key.VkCode == Native.VK_PACKET)
            {
                _ = harness.Guard.HoldAfterMatch(Target, 2, key);
            }
        };
        Assert.IsFalse(harness.Take(FakeTextBox.Marker(id)));
        harness.Guard.AwaitEnd(markerSent: true, []);
        Assert.IsTrue(harness.IsDown(Native.VK_LSHIFT));
        Assert.AreEqual(1, harness.Sent.Count, "The old insertion must not release a key still owned by the new match.");
        harness.Guard.ForegroundChanged(Other);
        harness.Guard.Dispose();
        Assert.IsFalse(harness.IsDown(Native.VK_LSHIFT));
        Assert.AreEqual("LShift-up", FakeTextBox.Describe(harness.Sent.Last()));
    }

    [TestMethod]
    public void DroppingAnOlderHeldUpDoesNotReleaseANewerUpHeldForAnotherInsertion()
    {
        using var harness = new Harness();
        Assert.IsTrue(harness.Take(Vk(Native.VK_LSHIFT)));
        Assert.IsTrue(harness.Guard.HoldAfterMatch(Target, 1, FakeTextBox.UserKey('x', up: false)));
        Assert.IsFalse(harness.Take(Vk(Native.VK_LSHIFT, up: true)));
        byte id = harness.Guard.Begin(2, Other);
        Assert.IsFalse(harness.Take(FakeTextBox.Marker(id)));
        Assert.IsFalse(harness.Take(Vk(Native.VK_LSHIFT)));
        Assert.IsFalse(harness.Take(Vk(Native.VK_LSHIFT, up: true)));
        harness.Guard.ForegroundChanged(Other);
        Assert.IsTrue(harness.IsDown(Native.VK_LSHIFT));
        Assert.AreEqual(0, harness.Sent.Count);
        harness.Guard.Drop();
        harness.Guard.Dispose();
        Assert.AreEqual("LShift-up", FakeTextBox.Describe(harness.Sent.Single()));
        Assert.IsFalse(harness.IsDown(Native.VK_LSHIFT));
    }

    [TestMethod]
    public void DisposalPumpsUntilTheReleaseSendHasReturned()
    {
        using var requested = new ManualResetEventSlim();
        using var taken = new ManualResetEventSlim();
        using var harness = new Harness();
        Native.INPUT[]? pending = null;
        bool returned = false;
        harness.Guard.Resend = keys =>
        {
            pending = [.. keys];
            requested.Set();
            _ = taken.Wait(10_000);
            returned = true;
            return (uint)keys.Length;
        };
        Assert.IsTrue(harness.Take(Vk(Native.VK_LSHIFT)));
        Assert.IsTrue(harness.Guard.HoldAfterMatch(Target, 1, FakeTextBox.UserKey('x', up: false)));
        Assert.IsFalse(harness.Take(Vk(Native.VK_LSHIFT, up: true)));
        int pumps = 0;
        harness.Guard.Dispose(() =>
        {
            pumps++;
            if (requested.IsSet && !taken.IsSet)
            {
                foreach (Native.INPUT key in pending!)
                {
                    _ = harness.Take(FakeTextBox.ToHookEvent(key));
                }

                taken.Set();
            }
        });
        Assert.IsTrue(pumps > 0);
        Assert.IsTrue(returned, "The hook cannot be removed while a release can still arrive.");
        Assert.IsFalse(harness.IsDown(Native.VK_LSHIFT));
        Assert.IsFalse(harness.Guard.HoldAfterMatch(Target, 2, FakeTextBox.UserKey('y', up: false)));
    }

    [TestMethod]
    public void DisposalAlsoAccountsForAReplayDetachedByAnotherWorker()
    {
        using var checking = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        using var harness = new Harness();
        harness.Guard.TargetInFront = _ =>
        {
            checking.Set();
            resume.Wait(10_000);
            return false;
        };
        Assert.IsTrue(harness.Take(Vk(Native.VK_LSHIFT)));
        Assert.IsTrue(harness.Guard.HoldAfterMatch(Target, 1, FakeTextBox.UserKey('x', up: false)));
        Assert.IsFalse(harness.Take(Vk(Native.VK_LSHIFT, up: true)));
        Task replay = Task.Run(() => harness.Guard.ReleaseAfterMatch(1));
        try
        {
            Assert.IsTrue(checking.Wait(10_000));
            harness.Guard.Dispose();
            Assert.IsFalse(harness.IsDown(Native.VK_LSHIFT));
            resume.Set();
            Assert.IsTrue(replay.Wait(10_000));
            Assert.AreEqual("LShift-up", FakeTextBox.Describe(harness.Sent.Single()));
        }
        finally
        {
            resume.Set();
            Assert.IsTrue(replay.Wait(10_000));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CleanupWaitsForReplayAndPairsAModifierRestoredByAShortSendDuringDisposal(bool shortSend)
    {
        using var sending = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        using var disposing = new ManualResetEventSlim();
        using var harness = new Harness();
        Assert.IsTrue(harness.Take(Vk(Native.VK_LSHIFT)));
        byte id = harness.Guard.Begin(1, Target);
        Assert.IsFalse(harness.Take(FakeTextBox.Marker(id)));
        Assert.IsFalse(harness.Take(Vk(0x41)));
        Assert.IsFalse(harness.Take(Vk(0x41, up: true)));
        Assert.IsFalse(harness.Take(Vk(Native.VK_LSHIFT, up: true)));
        Assert.IsTrue(harness.Take(Vk(Native.VK_LSHIFT, up: true) with { DwExtraInfo = KeyboardHook.InjectionSignature }));
        Assert.IsFalse(harness.Take(FakeTextBox.Marker(id)));
        bool shiftedA = false;
        harness.OnPassed = key =>
        {
            if (key.VkCode == 0x41 && (key.Flags & Native.LLKHF_UP) == 0)
            {
                shiftedA = harness.IsDown(Native.VK_LSHIFT);
            }
        };
        harness.Guard.Resend = keys =>
        {
            harness.Sent.Enqueue([.. keys]);
            if (keys[0].U.Ki.DwExtraInfo != KeyboardHook.InjectionSignature)
            {
                foreach (Native.INPUT key in keys)
                {
                    _ = harness.Take(FakeTextBox.ToHookEvent(key));
                }

                return (uint)keys.Length;
            }

            if (!shortSend)
            {
                _ = harness.Take(FakeTextBox.ToHookEvent(keys[0]));
            }

            sending.Set();
            _ = resume.Wait(10_000);
            if (shortSend)
            {
                _ = harness.Take(FakeTextBox.ToHookEvent(keys[0]));
                return 1;
            }

            foreach (Native.INPUT key in keys.Skip(1))
            {
                _ = harness.Take(FakeTextBox.ToHookEvent(key));
            }

            return (uint)keys.Length;
        };
        var pressAgain = new Native.INPUT
        {
            Type = Native.INPUT_KEYBOARD,
            U = new Native.InputUnion
            {
                Ki = new Native.KEYBDINPUT { WVk = Native.VK_LSHIFT, DwExtraInfo = KeyboardHook.InjectionSignature },
            },
        };
        Task replay = Task.Run(() => harness.Guard.AwaitEnd(markerSent: true, [pressAgain]));
        Task? disposal = null;
        try
        {
            Assert.IsTrue(sending.Wait(10_000));
            if (shortSend)
            {
                Assert.IsFalse(harness.IsDown(Native.VK_LSHIFT), "Disposal initially sees the modifier temporarily released.");
                disposal = Task.Run(() => harness.Guard.Dispose(() => disposing.Set()));
                Assert.IsTrue(disposing.Wait(10_000));
            }
            else
            {
                harness.Now = InsertionGuard.TimeoutMs;
                harness.Guard.Expire();
                Assert.IsTrue(harness.IsDown(Native.VK_LSHIFT));
                Assert.AreEqual(1, harness.Sent.Count, "Cleanup cannot overtake the held capital and its own release.");
            }

            resume.Set();
            Assert.IsTrue(replay.Wait(10_000));
            if (disposal is not null)
            {
                Assert.IsTrue(disposal.Wait(10_000));
            }

            harness.Guard.Dispose();
            Assert.IsFalse(harness.IsDown(Native.VK_LSHIFT));
            Assert.AreEqual(shortSend ? 2 : 1, harness.Sent.Count);
            Assert.AreEqual(!shortSend, shiftedA);
        }
        finally
        {
            resume.Set();
            Assert.IsTrue(replay.Wait(10_000));
            Assert.IsTrue(disposal is null || disposal.Wait(10_000));
        }
    }

    [TestMethod]
    public void TheNextInsertionCannotSampleAndRestoreAModifierWhoseDiscardedReleaseIsStillPending()
    {
        using var sending = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        using var beginning = new ManualResetEventSlim();
        using var harness = new Harness();
        harness.BeforeSend = _ =>
        {
            sending.Set();
            resume.Wait(10_000);
        };
        Assert.IsTrue(harness.Take(Vk(Native.VK_LSHIFT)));
        Assert.IsTrue(harness.Guard.HoldAfterMatch(Target, 1, FakeTextBox.UserKey('x', up: false)));
        Assert.IsFalse(harness.Take(Vk(Native.VK_LSHIFT, up: true)));
        harness.Guard.ForegroundChanged(Other);
        bool modifierAtBegin = true;
        Task? insertion = null;
        try
        {
            Assert.IsTrue(sending.Wait(10_000));
            insertion = Task.Run(() =>
            {
                beginning.Set();
                _ = harness.Guard.Begin(1, Other);
                modifierAtBegin = harness.IsDown(Native.VK_LSHIFT);
            });
            Assert.IsTrue(beginning.Wait(10_000));
            Assert.IsFalse(insertion.Wait(50), "The new worker must wait; the hook remains free to deliver input.");
            resume.Set();
            Assert.IsTrue(insertion.Wait(10_000));
            Assert.IsFalse(modifierAtBegin);
            Assert.AreEqual("LShift-up", FakeTextBox.Describe(harness.Sent.Single()));
        }
        finally
        {
            resume.Set();
            Assert.IsTrue(insertion is null || insertion.Wait(10_000));
        }
    }

    private static Native.KBDLLHOOKSTRUCT Vk(int vk, bool up = false) => FakeTextBox.UserVk(vk, up);

    private sealed class Harness : IDisposable
    {
        private readonly object _input = new();
        private readonly HashSet<uint> _down = [];

        public Harness(InsertionGuard? guard = null)
        {
            Guard = guard ?? new InsertionGuard(static () => 1, _ => InFront);
            Guard.Clock = () => Now;
            Guard.TargetInFront = _ => InFront;
            Guard.Resend = Send;
            Guard.ModifierIsDown = IsDown;
            Key = key => Guard.Swallows(key, observePassed: false);
        }

        public InsertionGuard Guard { get; }

        public Func<Native.KBDLLHOOKSTRUCT, bool> Key { get; set; }

        public Action<Native.INPUT[]>? BeforeSend { get; set; }

        public Action<Native.KBDLLHOOKSTRUCT>? OnPassed { get; set; }

        public ConcurrentQueue<Native.INPUT[]> Sent { get; } = new();

        public ConcurrentQueue<Native.KBDLLHOOKSTRUCT> Passed { get; } = new();

        public bool InFront { get; set; } = true;

        public long Now { get; set; }

        public int SendThread { get; private set; }

        public bool IsDown(int vk)
        {
            lock (_input)
            {
                return _down.Contains((uint)vk);
            }
        }

        public bool Take(Native.KBDLLHOOKSTRUCT key, bool downstreamAllows = true)
        {
            lock (_input)
            {
                if (Key(key) || !downstreamAllows || !Guard.Passed(key))
                {
                    return false;
                }

                if ((key.Flags & Native.LLKHF_UP) == 0)
                {
                    _down.Add(key.VkCode);
                }
                else
                {
                    _down.Remove(key.VkCode);
                }

                Passed.Enqueue(key);
                OnPassed?.Invoke(key);
                return true;
            }
        }

        public void Dispose() => Guard.Dispose();

        private uint Send(Native.INPUT[] keys)
        {
            SendThread = Environment.CurrentManagedThreadId;
            Sent.Enqueue([.. keys]);
            BeforeSend?.Invoke(keys);
            foreach (Native.INPUT key in keys)
            {
                _ = Take(FakeTextBox.ToHookEvent(key));
            }

            return (uint)keys.Length;
        }
    }
}
