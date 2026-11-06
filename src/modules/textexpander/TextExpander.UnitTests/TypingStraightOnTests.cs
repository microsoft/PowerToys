// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

/// <summary>
/// Typing straight on after a trigger, as the feature check did at 20 ms a key: the key after the trigger reached the app
/// before the insertion began, which dropped the expansion and lost the space the hook had swallowed. Every key goes
/// through the hook as it would with the hook installed, then into a text box, and the work ends as Program ends it.
/// </summary>
[TestClass]
public sealed class TypingStraightOnTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void KeysTypedStraightOnLandAfterTheSnippet(bool wordMode)
    {
        With(wordMode, "!abc=snippet", (hook, box) =>
        {
            box.UserTypes("!abc more");
            Assert.AreEqual(wordMode ? "Before !abc|" : "Before !ab|", box.ToString(), "The rest waits for the insertion.");
            Assert.IsTrue(Expand(hook, box));
            Assert.AreEqual("Before snippet more|", box.ToString());
        });
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TwoTriggersTypedBackToBackBothExpand(bool wordMode)
    {
        // The second matches as the keys held for the first go back, and holds what follows for its own insertion.
        With(wordMode, "!abc=snippet", (hook, box) =>
        {
            box.UserTypes("!abc !abc more");
            Assert.IsTrue(Expand(hook, box));
            Assert.AreEqual(wordMode ? "Before snippet !abc|" : "Before snippet !ab|", box.ToString());
            Assert.IsTrue(Expand(hook, box));
            Assert.AreEqual("Before snippet snippet more|", box.ToString());
            Assert.AreEqual(0, hook.Pending.Count);
        });
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void AClickBeforeTheInsertionLeavesWhatWasTypedAsTyped(bool wordMode)
    {
        // The caret may have moved, so nothing goes in: the swallowed key goes back first, then the keys typed after it.
        With(wordMode, "!abc=snippet", (hook, box) =>
        {
            box.UserTypes("!abc more");
            hook.Click();
            Assert.IsFalse(Expand(hook, box));
            Assert.AreEqual("Before !abc more|", box.ToString());
        });
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void WhatWasHeldGoesBackWhenTheInsertionIsTooLongComing(bool wordMode)
    {
        // The guard never keeps the keyboard long. Once those keys reached the app, the insertion doesn't go ahead.
        With(wordMode, "!abc=snippet", (hook, box) =>
        {
            long now = 0;
            hook.Guard.Clock = () => now;
            box.UserTypes("!abc more");
            now = InsertionGuard.TimeoutMs;
            hook.Guard.Expire();
            Assert.AreEqual("Before !abc more|", box.ToString());
            Assert.IsFalse(Expand(hook, box));
            Assert.AreEqual("Before !abc more|", box.ToString(), "Nothing went in, and nothing went back twice.");
        });
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ASlowInsertionWithNothingTypedOnStillGoesIn(bool wordMode)
    {
        // On a busy machine the worker can take more than 2 s to begin. With nothing typed on, nobody waits on the hold, so
        // the snippet still goes in, as it did before keys were held from the match on.
        With(wordMode, "!abc=snippet", (hook, box) =>
        {
            long now = 0;
            hook.Guard.Clock = () => now;
            box.UserTypes(wordMode ? "!abc " : "!abc");
            now = 10 * InsertionGuard.TimeoutMs;
            hook.Guard.Expire();
            Assert.IsTrue(Expand(hook, box));
            Assert.AreEqual(wordMode ? "Before snippet |" : "Before snippet|", box.ToString());
        });
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void KeysTypedOnLongAfterTheMatchStillWaitForTheWholeSnippet(bool wordMode)
    {
        // The worker began more than 2 s after the match, but within 2 s of the first key typed on. Counted from the match,
        // typing's hold was over at once, so those keys went in after the snippet's first character.
        With(wordMode, "!abc=snippet", (hook, box) =>
        {
            long now = 0;
            hook.Guard.Clock = () => now;
            box.UserTypes(wordMode ? "!abc " : "!abc");
            now = 10 * InsertionGuard.TimeoutMs;
            box.UserTypes(wordMode ? "more" : " more");
            Assert.IsTrue(Expand(hook, box));
            Assert.AreEqual("Before snippet more|", box.ToString());
        });
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void KeysTypedOnBeforeAQuestionCancelItAndStayAsTyped(bool wordMode)
    {
        // The question's window would have taken them.
        With(wordMode, "!abc=Hello {{prompt:Name}}", (hook, box) =>
        {
            box.UserTypes("!abc more");
            Assert.IsFalse(Expand(hook, box, ask: (_, _, _) =>
            {
                Assert.Fail("No window opens.");
                return null;
            }));
            Assert.AreEqual("Before !abc more|", box.ToString());
        });
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void WithNothingTypedOnAQuestionIsAskedAsBefore(bool wordMode)
    {
        // A Shift held through the trigger repeats while it's held, which isn't typing on.
        With(wordMode, "!abc=Hello {{prompt:Name}}", (hook, box) =>
        {
            box.UserTypes(wordMode ? "!abc " : "!abc");
            box.UserPresses((Native.VK_LSHIFT, false));
            int asked = 0;
            Assert.IsTrue(Expand(hook, box, ask: (_, _, accepted) =>
            {
                asked++;
                accepted?.Invoke();
                return "Ada";
            }));
            Assert.AreEqual(1, asked);
            Assert.AreEqual(wordMode ? "Before Hello Ada |" : "Before Hello Ada|", box.ToString());
        });
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(true, true)]
    [DataRow(false, false)]
    [DataRow(false, true)]
    public void WhenAnotherHookSwallowsTheMarkersTheSnippetGoesInUnlessKeysWereTypedOn(bool wordMode, bool typedOn)
    {
        // Without its markers an insertion goes in unguarded, as before keys were held from the match on, when the key the
        // hook swallowed counted as typed and cancelled every one. Keys typed on would be where the erase takes them.
        With(wordMode, "!abc=snippet", (hook, box) =>
        {
            hook.Guard.MarkerWaitMs = 50;
            box.UserTypes((wordMode ? "!abc " : "!abc") + (typedOn ? (wordMode ? "more" : " more") : string.Empty));
            Assert.AreEqual(!typedOn, Expand(hook, box, dispatch: WithoutMarkers(box)));
            Assert.AreEqual(typedOn ? "Before !abc more|" : wordMode ? "Before snippet |" : "Before snippet|", box.ToString());
        });
    }

    private static void With(bool wordMode, string snippet, Action<KeyboardHook, FakeTextBox> test)
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        File.WriteAllText(path, SnippetJson.Of(snippet));
        using var store = new SnippetStore(path);
        Assert.AreEqual(1, store.Load().Count);

        // Every key counts, as in a window that passed its check.
        using var hook = new KeyboardHook(store)
        {
            CaptureForeground = static () => KeyboardHook.ForegroundCapture.Authorized,
            InPasswordBox = static () => false,
            UserInput = static () => { },
        };
        using var box = new FakeTextBox("Before ", hook);
        bool required = WordBoundary.Required;
        InjectionBackend backend = Injector.Backend;
        try
        {
            WordBoundary.Required = wordMode;
            Injector.Backend = InjectionBackend.Typing;
            test(hook, box);
        }
        finally
        {
            WordBoundary.Required = required;
            Injector.Backend = backend;
        }
    }

    /// <summary>Runs the work the hook queued as Program does, typing into <paramref name="box"/>.</summary>
    private static bool Expand(KeyboardHook hook, FakeTextBox box, Func<PromptRequest, IntPtr, Action?, string?>? ask = null, Func<Native.INPUT[], uint>? dispatch = null)
    {
        Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion work));
        work = work with { TargetWindow = IntPtr.Zero };
        Program.ReplaceText insert = (backspaces, text, target, left, right, guard, generation, inserted, givenBack, fromClipboard, swallowed) => Injector.ReplaceCore(
            backspaces,
            text,
            target,
            left,
            _ => true,
            guard,
            generation,
            () => new IntPtr(1),
            box.Paste,
            dispatch ?? box.Dispatch,
            cursorRightUnits: right,
            getAsyncKeyState: _ => 0,
            sleep: _ => { },
            inserted: inserted,
            givenBack: givenBack,
            isOpenByTarget: box.TookPaste,
            isWindowsTerminal: _ => false,
            fromClipboard: fromClipboard,
            isConsole: _ => false,
            refusedInTerminal: (_, _) => Assert.Fail("No terminal refused anything."),
            swallowed: swallowed);
        return Program.Expand(hook, work, () => Program.TryExpand(hook, work, insert, isKeyDown: _ => false, ask: ask));
    }

    /// <summary>Sends as another hook that swallows the engine's markers lets through: the box and our hook never see them.</summary>
    private static Func<Native.INPUT[], uint> WithoutMarkers(FakeTextBox box) => inputs =>
    {
        Native.INPUT[] rest = [.. inputs.Where(input => input.U.Ki.WVk != InsertionGuard.MarkerVk)];
        if (rest.Length > 0)
        {
            _ = box.Dispatch(rest);
        }

        return (uint)inputs.Length;
    };
}
