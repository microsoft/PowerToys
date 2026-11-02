// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class InjectorDispatchTests
{
    [TestMethod]
    public void TypingSendsNewlinesAndTabsAsKeysAndASurrogatePairInOneCall()
    {
        // Real Enter and Tab presses, no \r, and the emoji's two halves together.
        using var box = new FakeTextBox(string.Empty);
        Assert.IsTrue(Replace(box, 0, "\r\n\tA😀", InjectionBackend.Typing, box.Dispatch));

        Assert.AreEqual("Mark | Enter | \t | A | Mark", string.Join(" | ", box.Batches.Where(batch => batch.Length != 4).Select(FakeTextBox.Describe)));
        Native.INPUT[] emoji = box.Batches.Single(batch => batch.Length == 4);
        Assert.AreEqual("😀", string.Concat(emoji.Where(input => (input.U.Ki.DwFlags & Native.KEYEVENTF_KEYUP) == 0).Select(input => (char)input.U.Ki.WScan)));
        Assert.IsTrue(box.Batches.SelectMany(batch => batch).All(input => input.U.Ki.DwExtraInfo == KeyboardHook.InjectionSignature), "Our own input, which the hook ignores.");
        Assert.AreEqual("\nA😀|", box.ToString());
    }

    [TestMethod]
    public void APartialSendEndsTheGuardAndNeverLeavesCtrlDown()
    {
        InjectionDiagnostics.Reset();
        using var box = new FakeTextBox("Before !abc") { Clipboard = "the user's" };
        int calls = 0;

        // Nothing went out, not even the start marker: nothing to end or let go of.
        Assert.IsFalse(Replace(box, 2, "EXPANDED", InjectionBackend.Clipboard, _ =>
        {
            calls++;
            return 0;
        }));
        Assert.AreEqual(1, calls);
        Assert.IsFalse(box.Guard.Swallows(FakeTextBox.UserKey('z', up: false)));

        // Cut off after Ctrl went down: it's let go of, and the end marker ends the guard.
        Assert.IsFalse(Replace(box, 2, "EXPANDED", InjectionBackend.Clipboard, inputs =>
        {
            Native.INPUT[] sent = inputs.Length == 4 && inputs[1].U.Ki.WVk == Native.VK_V ? inputs[..2] : inputs;
            _ = box.Dispatch(sent);
            return (uint)sent.Length;
        }));
        Assert.AreEqual("Mark | BS BS | Ctrl-down V-down | Ctrl-up | Mark", string.Join(" | ", box.Batches.Select(FakeTextBox.Describe)));
        Assert.AreEqual("Before !aEXPANDED|", box.ToString());
        Assert.AreEqual("the user's", box.Clipboard);
        Assert.IsFalse(box.Guard.Swallows(FakeTextBox.UserKey('z', up: false)));
        Assert.AreEqual(2, InjectionDiagnostics.SendInputFailures);
        InjectionDiagnostics.Reset();
    }

    [TestMethod]
    public void AFailureAfterEraseIsTerminalAndCannotRetainUndo()
    {
        InjectionBackend originalBackend = Injector.Backend;
        using var guard = new InsertionGuard(() => 0, _ => true) { MarkerWaitMs = 0 };
        bool recorded = false;
        try
        {
            InjectionDiagnostics.Reset();
            Injector.Backend = InjectionBackend.Typing;
            Assert.IsFalse(Injector.ReplaceCore(
                3,
                "replacement",
                IntPtr.Zero,
                2,
                _ => true,
                guard,
                0,
                () => new IntPtr(1),
                dispatch: inputs => inputs.Length > 1 ? (uint)inputs.Length - 1 : (uint)inputs.Length,
                getAsyncKeyState: _ => 0,
                sleep: _ => { },
                inserted: () => recorded = true));
            Assert.AreEqual(0, InjectionDiagnostics.Typed);

            Injector.Backend = InjectionBackend.Clipboard;
            bool typedFallback = false;
            Assert.IsFalse(Injector.ReplaceCore(
                3,
                "replacement",
                IntPtr.Zero,
                0,
                _ => true,
                guard,
                0,
                () => new IntPtr(1),
                (_, _, _, _) => PasteOutcome.FailedAfterErase,
                inputs =>
                {
                    typedFallback |= inputs[0].U.Ki.WVk != InsertionGuard.MarkerVk;
                    return (uint)inputs.Length;
                },
                inserted: () => recorded = true));
            Assert.IsFalse(typedFallback);
            Assert.AreEqual(0, InjectionDiagnostics.Typed);
            Assert.AreEqual(1, InjectionDiagnostics.Abandoned);
            Assert.IsFalse(recorded, "No undo record for text that may not be there.");
        }
        finally
        {
            InjectionDiagnostics.Reset();
            Injector.Backend = originalBackend;
        }
    }

    [TestMethod]
    public void ASlowClipboardLossHandlerDoesntHoldUpTheWorker()
    {
        // Its message box, opened on the worker, could sit behind other windows: until someone closed it, every trigger
        // swallowed its space and inserted nothing.
        Action<string>? original = Injector.ClipboardRestoreFailed;
        var shown = new TaskCompletionSource();
        var closed = new TaskCompletionSource();
        try
        {
            Injector.ClipboardRestoreFailed = _ =>
            {
                shown.TrySetResult();
                closed.Task.Wait();
            };
            Assert.IsTrue(Task.Run(Injector.ReportClipboardLoss).Wait(10_000), "The worker waited on the message box.");
            Assert.IsTrue(shown.Task.Wait(10_000), "Reported all the same.");
        }
        finally
        {
            closed.TrySetResult();
            Injector.ClipboardRestoreFailed = original;
        }
    }

    private static bool Replace(FakeTextBox box, int erase, string text, InjectionBackend backend, Func<Native.INPUT[], uint> dispatch)
    {
        InjectionBackend original = Injector.Backend;
        try
        {
            box.Batches.Clear();
            Injector.Backend = backend;
            return Injector.ReplaceCore(erase, text, IntPtr.Zero, 0, _ => true, box.Guard, box.Hook.InputGeneration, () => new IntPtr(1), box.Paste, dispatch, getAsyncKeyState: _ => 0, sleep: _ => { });
        }
        finally
        {
            Injector.Backend = original;
        }
    }
}
