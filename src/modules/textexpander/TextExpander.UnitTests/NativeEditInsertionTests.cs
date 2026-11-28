// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class NativeEditInsertionTests
{
    [TestMethod]
    public void FullDispatchDoesNotMakeClippedInsertionUndoable()
    {
        using var target = new EditTarget("ABCDE!x");
        target.Send(0x00C5, 8); // EM_SETLIMITTEXT
        NativeEdit.Undo? recorded = null;

        Assert.IsTrue(target.Replace("!x", "1234 ", recordUndo: undo => recorded = undo));
        Assert.AreEqual("ABCDE123", target.Read().Text);
        Assert.IsNull(recorded, "Accepting every dispatched event does not mean every character fit in the control.");
    }

    [TestMethod]
    public void RightToLeftCaretAndUndoUseTheLogicalOffsets()
    {
        using var target = new EditTarget("Prefix !x", rightToLeft: true);
        NativeEdit.Undo? recorded = null;

        Assert.IsTrue(target.Replace("!x", "אבגד", caretSuffix: "גד", recordUndo: undo => recorded = undo));
        Assert.AreEqual(new NativeEdit.Snapshot("Prefix אבגד", 9, 9), target.Read());
        Assert.IsNotNull(recorded);

        Assert.IsTrue(target.Replace(string.Empty, "!x", undo: recorded));
        Assert.AreEqual("Prefix !x", target.Read().Text);
    }

    [TestMethod]
    public void ACombiningTriggerIsErasedAsAnExactRange()
    {
        using var target = new EditTarget("Prefix !e\u0301");

        Assert.IsTrue(target.Replace("!e\u0301", "expanded"));
        Assert.AreEqual("Prefix expanded", target.Read().Text);
    }

    [TestMethod]
    public void AnUndoWithChangedSurroundingTextDoesNotEraseAnything()
    {
        using var target = new EditTarget("Prefix !x");
        NativeEdit.Undo? recorded = null;
        Assert.IsTrue(target.Replace("!x", "text", recordUndo: undo => recorded = undo));
        Assert.IsNotNull(recorded);

        using var changed = new EditTarget("CHANGE text");
        Assert.IsFalse(changed.Replace(string.Empty, "!x", undo: recorded));
        Assert.AreEqual("CHANGE text", changed.Read().Text);
    }

    [TestMethod]
    public void AFocusChangeWithNoNewKeyCancelsTheOperation()
    {
        using var target = new EditTarget("Prefix !x");
        int focusChecks = 0;
        bool TargetStillFocused() => ++focusChecks <= 3;

        Assert.IsFalse(target.Replace("!x", "expanded", focusUnchanged: TargetStillFocused));
        Assert.AreEqual(4, focusChecks);
        Assert.AreEqual("Prefix !x", target.Read().Text);
    }

    /// <summary>A hidden native Edit with test-only input delivery. No global input or clipboard operations.</summary>
    private sealed class EditTarget : IDisposable
    {
        private readonly IntPtr _window;
        private readonly NativeEdit _edit;
        private readonly InsertionGuard _guard;

        public EditTarget(string text, bool rightToLeft = false)
        {
            const int wsPopup = unchecked((int)0x80000000);
            const int wsExRtlReading = 0x00002000;
            _window = Native.CreateWindowExW(
                rightToLeft ? wsExRtlReading : 0,
                "Edit",
                text,
                wsPopup | Native.ES_AUTOHSCROLL,
                0,
                0,
                200,
                30,
                IntPtr.Zero,
                IntPtr.Zero,
                Native.GetModuleHandleW(null),
                IntPtr.Zero);
            Assert.AreNotEqual(IntPtr.Zero, _window);
            _edit = NativeEdit.ForWindow(_window)!;
            Assert.IsNotNull(_edit);
            Assert.IsTrue(_edit.Select(text.Length, text.Length));
            _guard = new(static () => 0, static _ => true) { Resend = Dispatch };
        }

        public NativeEdit.Snapshot Read() => _edit.Read()!.Value;

        public bool Replace(string typedPrefix, string text, string caretSuffix = "", NativeEdit.Undo? undo = null, Action<NativeEdit.Undo>? recordUndo = null, Func<bool>? focusUnchanged = null)
        {
            InjectionBackend backend = Injector.Backend;
            try
            {
                Injector.Backend = InjectionBackend.Typing;
                return Injector.ReplaceCore(
                    Injector.CountDeletableUnits(undo?.Text ?? typedPrefix),
                    text,
                    _window,
                    Injector.CountDeletableUnits(caretSuffix),
                    static _ => true,
                    _guard,
                    0,
                    () => _window,
                    dispatch: Dispatch,
                    getAsyncKeyState: static _ => 0,
                    sleep: static _ => { },
                    isWindowsTerminal: static _ => false,
                    isConsole: static _ => false,
                    nativeEdit: new(_edit, typedPrefix, caretSuffix, undo, recordUndo),
                    focusUnchanged: focusUnchanged);
            }
            finally
            {
                Injector.Backend = backend;
            }
        }

        public void Send(uint message, int value)
        {
            Assert.AreNotEqual(
                IntPtr.Zero,
                Native.SendMessageTimeoutW(_window, message, new IntPtr(value), IntPtr.Zero, Native.SMTO_BLOCK, 50, out _));
        }

        public void Dispose()
        {
            _guard.Dispose();
            _ = Native.DestroyWindow(_window);
        }

        private uint Dispatch(Native.INPUT[] inputs)
        {
            const uint wmChar = 0x0102;
            foreach (Native.INPUT input in inputs)
            {
                Native.KBDLLHOOKSTRUCT key = FakeTextBox.ToHookEvent(input);
                if (_guard.Swallows(key) || (key.Flags & Native.LLKHF_UP) != 0)
                {
                    continue;
                }

                Assert.AreEqual((uint)Native.VK_PACKET, key.VkCode, "The native range does not use physical Backspace or cursor keys.");
                Send(wmChar, (int)key.ScanCode);
            }

            return (uint)inputs.Length;
        }
    }
}
