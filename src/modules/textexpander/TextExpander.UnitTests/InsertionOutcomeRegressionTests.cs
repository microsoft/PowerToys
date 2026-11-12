// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class InsertionOutcomeRegressionTests
{
    [TestMethod]
    public void TerminalUndoUsesTheTextActuallyTyped()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        File.WriteAllText(path, SnippetJson.Of("!x=ab\u000Fcd"));
        using var store = new SnippetStore(path);
        Assert.AreEqual(1, store.Load().Count);
        using var hook = new KeyboardHook(store)
        {
            CaptureForeground = static () => KeyboardHook.ForegroundCapture.Authorized,
            InPasswordBox = static () => false,
            UserInput = static () => { },
        };
        using var box = new FakeTextBox("PREFIX ", hook);
        bool required = WordBoundary.Required;
        InjectionBackend backend = Injector.Backend;
        try
        {
            WordBoundary.Required = true;
            Injector.Backend = InjectionBackend.Typing;
            box.UserTypes("!x ");
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
            work = work with { TargetWindow = IntPtr.Zero };
            Program.ReplaceText replace = (erase, text, target, left, right, guard, generation, inserted, givenBack, fromClipboard, swallowed) => Injector.ReplaceCore(
                erase, text, target, left, _ => true, guard, generation,
                () => new IntPtr(1), box.Paste, box.Dispatch,
                cursorRightUnits: right, getAsyncKeyState: _ => 0, sleep: _ => { },
                inserted: inserted, givenBack: givenBack, isWindowsTerminal: _ => true,
                fromClipboard: fromClipboard, isConsole: _ => false, swallowed: swallowed);

            Assert.IsTrue(Program.Expand(hook, work, () => Program.TryExpand(hook, work, replace)));
            Assert.AreEqual("PREFIX abcd ", box.Text);
            Assert.IsTrue(hook.ProcessKey((uint)'Z', 0, ctrl: true, alt: false, shift: false, win: false));
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion undo));
            Assert.AreEqual("abcd ", undo.Replacement);
            Assert.IsTrue(Program.Expand(hook, undo, () => Program.TryExpand(hook, undo, replace, isKeyDown: _ => false)));
            Assert.AreEqual("PREFIX !x ", box.Text);
        }
        finally
        {
            WordBoundary.Required = required;
            Injector.Backend = backend;
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PasteDispatchIsSeparateFromEraseAndModifierInput(bool acceptPaste)
    {
        using var box = new FakeTextBox("PREFIX !x");
        InjectionBackend backend = Injector.Backend;
        try
        {
            Injector.Backend = InjectionBackend.Clipboard;
            (bool Whole, bool SentAny, bool PasteQueued) observed = default;
            uint Dispatch(Native.INPUT[] inputs)
            {
                if (!acceptPaste && inputs.Any(input => input.U.Ki.WVk == Native.VK_CONTROL && (input.U.Ki.DwFlags & Native.KEYEVENTF_KEYUP) == 0))
                {
                    box.Hook.Click();
                }

                return box.Dispatch(inputs);
            }

            bool completed = Injector.ReplaceCore(
                2, "SNIPPET", IntPtr.Zero, 0, _ => true, box.Guard, box.Hook.InputGeneration,
                () => new IntPtr(1),
                (text, target, send, keepAlive) => box.Paste(text, target, () => observed = send(), keepAlive),
                Dispatch, getAsyncKeyState: _ => 0, sleep: _ => { },
                isOpenByTarget: box.TookPaste, isWindowsTerminal: _ => false, isConsole: _ => false);

            Assert.AreEqual(acceptPaste, completed);
            Assert.IsTrue(observed.SentAny, "Erasing the trigger is input even when no paste was queued.");
            Assert.AreEqual(acceptPaste, observed.PasteQueued);
        }
        finally
        {
            Injector.Backend = backend;
        }
    }
}
