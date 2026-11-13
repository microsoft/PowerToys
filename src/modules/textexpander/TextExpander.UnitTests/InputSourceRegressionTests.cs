// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class InputSourceRegressionTests
{
    private static readonly bool[] KeyDownThenUp = [false, true];

    [TestMethod]
    [DataRow("ab", "PREFIXexpanded")]
    [DataRow("b", "PREFIXaexpanded")]
    public void OnePhysicalKeysCompleteOutputIsPreservedThroughExpansionAndUndo(string trigger, string expected)
    {
        With(
            false,
            [trigger + "=expanded"],
            (hook, box) =>
            {
                box.UserPresses((0x34, false), (0x34, true));
                Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
                Assert.AreEqual("ab", work.SwallowedText);
                Assert.AreEqual(string.Empty, work.TypedPrefix, "The whole physical event was swallowed.");
                Assert.IsTrue(Expand(hook, box, work));
                Assert.AreEqual(expected, box.Text);

                Assert.IsTrue(hook.ProcessKey(Native.VK_Z, 0, ctrl: true, alt: false, shift: false, win: false));
                Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion undo));
                Assert.IsTrue(Expand(hook, box, undo));
                Assert.AreEqual("PREFIXab", box.Text, "Undo also restores the same key's unmatched literal prefix.");
            },
            decode: TwoCharacters);
    }

    [TestMethod]
    [DataRow("ab")]
    [DataRow("b")]
    public void CancellingAPromptReturnsTheWholePhysicalKey(string trigger)
    {
        With(
            false,
            [trigger + "={{prompt:Name}}"],
            (hook, box) =>
            {
                box.UserPresses((0x34, false), (0x34, true));
                Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
                Assert.IsFalse(Expand(hook, box, work, ask: (_, _, _) => null));
                Assert.AreEqual("PREFIXab", box.Text);
                Assert.AreEqual(0, hook.Pending.Count);
            },
            decode: TwoCharacters);
    }

    [TestMethod]
    public void APhysicalKeyWithMoreThanTwoCharactersCanFormAWordWaitTrigger()
    {
        With(
            true,
            ["abcd=expanded"],
            (hook, box) =>
            {
                box.AfterKey = key =>
                {
                    if (key.VkCode == 0x34 && (key.Flags & Native.LLKHF_UP) == 0)
                    {
                        box.Insert("abcd");
                    }
                };
                box.UserPresses((0x34, false), (0x34, true));
                box.UserTypes(" ");
                Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
                Assert.AreEqual("abcd", work.TypedPrefix);
                Assert.AreEqual(" ", work.SwallowedText);
                Assert.IsTrue(Expand(hook, box, work));
                Assert.AreEqual("PREFIXexpanded ", box.Text);
            },
            decode: FourCharacters);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OnlyTheKeyboardManagerReplacementCountsInEitherHookOrder(bool remapBeforeOurHook)
    {
        With(true, ["!ab=phantom", "!bb=valid"], (hook, box) =>
        {
            box.Remap = (0x41, 0x42);
            box.AfterKey = key =>
            {
                if (key.VkCode == 0x42 && (key.Flags & Native.LLKHF_UP) == 0)
                {
                    box.Insert("b");
                }
            };
            void A()
            {
                foreach (bool up in KeyDownThenUp)
                {
                    Native.KBDLLHOOKSTRUCT key = FakeTextBox.UserVk(remapBeforeOurHook ? 0x42 : 0x41, up);
                    box.Take(remapBeforeOurHook ? key with { Flags = key.Flags | 0x10, DwExtraInfo = 0x11 } : key);
                }
            }

            box.UserTypes("!");
            A();
            box.UserTypes(" ");
            Assert.AreEqual(0, hook.Pending.Count, "The suppressed A cannot form a phantom !ab.");
            Assert.AreEqual("PREFIX!b ", box.Text);

            box.UserTypes("!");
            A();
            A();
            box.UserTypes(" ");
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
            Assert.AreEqual("!bb", work.TypedPrefix);
            Assert.IsTrue(Expand(hook, box, work));
            Assert.AreEqual("PREFIX!b valid ", box.Text);
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void QuickAccentReplacementUpdatesTheTriggerInsteadOfKeepingTheBaseLetter(bool accentedTrigger)
    {
        string[] snippets = accentedTrigger ? ["!c=base", "!ç=accented"] : ["!c=base"];
        With(true, snippets, (hook, box) =>
        {
            box.UserTypes("!c");
            TakeQuickAccentKey(box, FakeTextBox.UserVk(Native.VK_BACK, up: false));
            TakeQuickAccentKey(box, FakeTextBox.UserKey('ç', up: false));
            Assert.AreEqual("PREFIX!ç", box.Text);

            box.UserTypes(" ");
            Assert.AreEqual(accentedTrigger, hook.Pending.TryTake(out PendingExpansion work), "The old !c trigger no longer matches.");
            if (accentedTrigger)
            {
                Assert.AreEqual("!ç", work.Trigger, "The character Quick Accent inserted can form its own trigger.");
                Assert.IsTrue(Expand(hook, box, work));
                Assert.AreEqual("PREFIXaccented ", box.Text);
            }
            else
            {
                Assert.AreEqual("PREFIX!ç ", box.Text);
            }
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public unsafe void QuickAccentReturnedSpaceExpandsOnceInEitherHookOrder(bool quickAccentFirst)
    {
        With(true, ["!c=expanded"], (hook, box) =>
        {
            box.UserTypes("!c");
            if (!quickAccentFirst)
            {
                Native.HookProc next = hook.NextHook;
                hook.NextHook = (code, message, data) =>
                    message == new IntPtr(Native.WM_KEYDOWN) && ((Native.KBDLLHOOKSTRUCT*)data)->VkCode == 0x20
                        ? new IntPtr(1)
                        : next(code, message, data);
                box.UserPresses((0x20, false));
            }

            // Quick Accent consumes the physical Space, then returns its text when C was released too quickly.
            box.UserPresses((0x20, true));
            TakeQuickAccentKey(box, FakeTextBox.UserKey(' ', up: false));
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work), "The returned Space releases the trigger now.");
            Assert.AreEqual("!c", work.TypedPrefix);
            Assert.IsTrue(Expand(hook, box, work));
            Assert.AreEqual("PREFIXexpanded ", box.Text);

            box.UserTypes(" ");
            Assert.AreEqual(0, hook.Pending.Count, "A later Space cannot match the old trigger again.");
            Assert.AreEqual("PREFIXexpanded  ", box.Text);
        });
    }

    [TestMethod]
    public void RunnerDummyKeyUpsLeaveMatchingAndUndoIntact()
    {
        With(true, ["!c=expanded"], (hook, box) =>
        {
            Native.KBDLLHOOKSTRUCT marker = FakeTextBox.UserVk(InsertionGuard.MarkerVk, up: true) with
            {
                Flags = 0x10 | Native.LLKHF_UP,
                DwExtraInfo = HostInputTags.CentralizedHookDontTrigger,
            };
            box.UserTypes("!");
            box.Take(marker);
            box.UserTypes("c ");
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
            Assert.IsTrue(Expand(hook, box, work));
            Assert.AreEqual("PREFIXexpanded ", box.Text);

            box.Take(marker);
            Assert.IsTrue(hook.ProcessKey(Native.VK_Z, 0, ctrl: true, alt: false, shift: false, win: false));
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion undo));
            Assert.IsTrue(Expand(hook, box, undo));
            Assert.AreEqual("PREFIX!c ", box.Text);
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ACancelledSingleCharacterIsReturnedOnceWithoutDisablingLaterTyping(bool remapped)
    {
        With(false, ["x=EXP"], (hook, box) =>
        {
            if (remapped)
            {
                box.Remap = (0x41, 0x58);
                box.AfterKey = key =>
                {
                    if (key.VkCode == 0x58 && (key.Flags & Native.LLKHF_UP) == 0)
                    {
                        box.Insert("x");
                    }
                };
            }

            void X()
            {
                if (remapped)
                {
                    box.UserPresses((0x41, false), (0x41, true));
                }
                else
                {
                    box.UserTypes("x");
                }
            }

            X();
            box.UserTypes("y");
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion cancelled));
            hook.Click();
            Assert.IsFalse(Expand(hook, box, cancelled));
            Assert.AreEqual("PREFIXxy", box.Text);
            Assert.AreEqual(0, hook.Pending.Count, "Returning the matching event must not queue it again.");

            X();
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion next));
            Assert.IsTrue(Expand(hook, box, next));
            Assert.AreEqual("PREFIXxyEXP", box.Text, "A new user event still matches, with remaps applied once.");
        });
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void ATriggerReplayedAtTheTypingLimitStaysLiteralAndStopsTheInsertion(bool missingDrainMarker, bool remapped)
    {
        With(true, ["!long=" + new string('x', 100), "!b=SECOND"], (hook, box) =>
        {
            long now = 0;
            hook.Guard.Clock = () => now;
            if (missingDrainMarker)
            {
                hook.Guard.MarkerWaitMs = 0;
                hook.Guard.Resend = keys =>
                {
                    _ = box.Dispatch([.. keys.Where(key => key.U.Ki.WVk != InsertionGuard.MarkerVk)]);
                    return (uint)keys.Length;
                };
            }

            box.UserTypes("!long !");
            if (remapped)
            {
                box.Remap = (0x41, 0x42);
                box.AfterKey = key =>
                {
                    if (key.VkCode == 0x42 && (key.Flags & Native.LLKHF_UP) == 0)
                    {
                        box.Insert("b");
                    }
                };
                box.UserPresses((0x41, false), (0x41, true));
            }
            else
            {
                box.UserTypes("b");
            }

            box.UserTypes(" ");
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
            Assert.IsFalse(Expand(hook, box, work, sleep: ms => now += ms));
            Assert.AreEqual(0, hook.Pending.Count, "Returning held input must not start another expansion inside the partial snippet.");
            Assert.IsTrue(box.Text.Contains("!b ", StringComparison.Ordinal));
            Assert.IsTrue(box.Text.Count(ch => ch == 'x') is > 0 and < 100, "Returned input stops the rest, whether or not its drain marker arrives.");
        });
    }

    private static void TakeQuickAccentKey(FakeTextBox box, Native.KBDLLHOOKSTRUCT key)
    {
        foreach (bool up in KeyDownThenUp)
        {
            box.Take(key with
            {
                Flags = key.Flags | 0x10 | (up ? Native.LLKHF_UP : 0),
                DwExtraInfo = HostInputTags.CentralizedHookDontTrigger,
            });
        }
    }

    private static void With(bool wordMode, string[] snippets, Action<KeyboardHook, FakeTextBox> test, KeyboardHook.KeyDecoder? decode = null)
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        File.WriteAllText(path, SnippetJson.Of(snippets));
        using var store = new SnippetStore(path);
        Assert.IsTrue(store.Load().Succeeded);
        using var hook = new KeyboardHook(store)
        {
            CaptureForeground = static () => KeyboardHook.ForegroundCapture.Authorized,
            InPasswordBox = static () => false,
            UserInput = static () => { },
            Decode = decode ?? PacketOrLetter,
        };
        using var box = new FakeTextBox("PREFIX", hook);
        bool required = WordBoundary.Required;
        bool keep = WordBoundary.KeepTerminator;
        bool remaps = HostInputTags.AcceptRemappedInput;
        InjectionBackend backend = Injector.Backend;
        try
        {
            WordBoundary.Required = wordMode;
            WordBoundary.KeepTerminator = true;
            HostInputTags.AcceptRemappedInput = true;
            Injector.Backend = InjectionBackend.Typing;
            test(hook, box);
        }
        finally
        {
            WordBoundary.Required = required;
            WordBoundary.KeepTerminator = keep;
            HostInputTags.AcceptRemappedInput = remaps;
            Injector.Backend = backend;
        }
    }

    private static bool Expand(KeyboardHook hook, FakeTextBox box, PendingExpansion pending, Func<PromptRequest, IntPtr, Action?, string?>? ask = null, Action<int>? sleep = null)
    {
        PendingExpansion work = pending with { TargetWindow = IntPtr.Zero };
        Program.ReplaceText replace = (erase, text, target, left, right, guard, generation, inserted, givenBack, fromClipboard, swallowed) => Injector.ReplaceCore(
            erase,
            text,
            target,
            left,
            _ => true,
            guard,
            generation,
            getForegroundWindow: () => new IntPtr(1),
            tryPaste: box.Paste,
            dispatch: box.Dispatch,
            cursorRightUnits: right,
            getAsyncKeyState: _ => 0,
            sleep: sleep ?? (_ => { }),
            inserted: inserted,
            givenBack: givenBack,
            isOpenByTarget: box.TookPaste,
            isWindowsTerminal: _ => false,
            fromClipboard: fromClipboard,
            isConsole: _ => false,
            swallowed: swallowed);
        return Program.Expand(hook, work, () => Program.TryExpand(hook, work, replace, isKeyDown: _ => false, ask: ask));
    }

    private static int TwoCharacters(uint vk, uint scan, Span<char> typed)
    {
        if (vk != 0x34)
        {
            return PacketOrLetter(vk, scan, typed);
        }

        "ab".AsSpan().CopyTo(typed);
        return 2;
    }

    private static int FourCharacters(uint vk, uint scan, Span<char> typed)
    {
        if (vk != 0x34)
        {
            return PacketOrLetter(vk, scan, typed);
        }

        "abcd".AsSpan().CopyTo(typed);
        return 4;
    }

    private static int PacketOrLetter(uint vk, uint scan, Span<char> typed)
    {
        if (vk == Native.VK_PACKET)
        {
            typed[0] = (char)scan;
            return 1;
        }

        if (vk is >= 'A' and <= 'Z')
        {
            typed[0] = (char)(vk + ('a' - 'A'));
            return 1;
        }

        return 0;
    }
}
