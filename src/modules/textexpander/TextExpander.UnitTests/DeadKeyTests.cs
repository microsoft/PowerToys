// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

/// <summary>
/// Dead keys, as on French AZERTY's ^ key: the key types nothing, and the app composes the next key with its accent. The
/// hook reads the composed character only once the app has taken the dead key, so both timings are covered.
/// </summary>
[TestClass]
public sealed class DeadKeyTests
{
    private const uint DeadCircumflex = 0xDD;
    private const uint Space = 0x20;
    private const uint Exclamation = 0xDF;

    [TestMethod]
    [DataRow(true, DisplayName = "The hook reads the app's accent")]
    [DataRow(false, DisplayName = "The app hasn't taken the dead key yet")]
    public void AnAccentTypedWithADeadKeyAfterATriggerDoesNotReleaseIt(bool readsAccent)
    {
        // The space after the dead key types the accent. Swallowed to release !qwazy, it left the accent waiting in the
        // app, which took the erase's first Backspace: "!Layout letters OK^", the trigger's first character left behind.
        WordMode(["!qwazy=Layout letters OK"], readsAccent, (hook, layout) =>
        {
            layout.Type(hook, "!qwazy");
            Assert.IsFalse(layout.Press(hook, DeadCircumflex), "A dead key types nothing yet.");
            Assert.IsFalse(layout.Press(hook, Space), "The space the accent is typed with releases nothing.");
            Assert.AreEqual(0, hook.Pending.Count);

            // The accent belongs to the word now, so a space after it doesn't release the trigger either.
            Assert.IsFalse(layout.Press(hook, Space));
            Assert.AreEqual(0, hook.Pending.Count);
        });
    }

    [TestMethod]
    public void ATriggerTypedWithAnAccentFromADeadKeyStillExpands()
    {
        WordMode(["^up=Caret trigger OK"], readsAccent: true, (hook, layout) =>
        {
            Assert.IsFalse(layout.Press(hook, DeadCircumflex));
            Assert.IsFalse(layout.Press(hook, Space), "The space types the accent.");
            layout.Type(hook, "up");
            Assert.IsTrue(layout.Press(hook, Space), "A space typed on its own releases the trigger.");
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion expansion));
            Assert.AreEqual("^up", expansion.Trigger);
            Assert.AreEqual("^up ", expansion.TypedText);
        });
    }

    [TestMethod]
    public void AnAccentThatDoesNotCombineKeepsBothCharacters()
    {
        // ^ then x types "^x": both reach the app, so both count.
        WordMode(["^x=Both characters"], readsAccent: true, (hook, layout) =>
        {
            Assert.IsFalse(layout.Press(hook, DeadCircumflex));
            Assert.IsFalse(layout.Press(hook, 'X'));
            Assert.IsTrue(layout.Press(hook, Space));
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion expansion));
            Assert.AreEqual("^x", expansion.Trigger);
        });
    }

    [TestMethod]
    public void BackspaceAfterADeadKeyOnlyDropsTheAccent()
    {
        WordMode(["!qwazy=Layout letters OK"], readsAccent: true, (hook, layout) =>
        {
            layout.Type(hook, "!qwaz");
            Assert.IsFalse(layout.Press(hook, DeadCircumflex));
            Assert.IsFalse(layout.Press(hook, Native.VK_BACK), "Backspace drops the accent, and no character with it.");
            layout.Type(hook, "y");
            Assert.IsTrue(layout.Press(hook, Space));
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion expansion));
            Assert.AreEqual("!qwazy", expansion.Trigger);
        });
    }

    [TestMethod]
    public void ALetterComposedWithAnAccentIsNotThePlainLetter()
    {
        WordMode(["!ete=Accent case"], readsAccent: true, (hook, layout) =>
        {
            layout.Type(hook, "!et");
            Assert.IsFalse(layout.Press(hook, DeadCircumflex));
            Assert.IsFalse(layout.Press(hook, 'E'), "^ then e types ê.");
            Assert.IsFalse(layout.Press(hook, Space), "!etê isn't !ete.");
            Assert.AreEqual(0, hook.Pending.Count);
        });
    }

    private static void WordMode(string[] snippets, bool readsAccent, Action<KeyboardHook, DeadKeyLayout> test)
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        File.WriteAllText(path, SnippetJson.Of(snippets));
        using var store = new SnippetStore(path);
        Assert.AreEqual(snippets.Length, store.Load().Count);
        var layout = new DeadKeyLayout(readsAccent);
        using var hook = new KeyboardHook(store) { Decode = layout.Decode };

        bool original = WordBoundary.Required;
        try
        {
            WordBoundary.Required = true;
            test(hook, layout);
        }
        finally
        {
            WordBoundary.Required = original;
        }
    }

    /// <summary>
    /// A layout with a dead circumflex, and the app it types into: the app keeps the accent for the next key. With
    /// <paramref name="readsAccent"/> false, the hook reads keys before the app has taken the dead key.
    /// </summary>
    private sealed class DeadKeyLayout(bool readsAccent)
    {
        private bool pending;

        public bool Press(KeyboardHook hook, uint vk)
        {
            if (vk == Native.VK_BACK)
            {
                pending = false;
            }

            return hook.ProcessKey(vk, 0, ctrl: false, alt: false, shift: false, win: false);
        }

        public void Type(KeyboardHook hook, string text)
        {
            foreach (char ch in text)
            {
                Assert.IsFalse(Press(hook, ch == '!' ? Exclamation : char.ToUpperInvariant(ch)), $"'{ch}'");
            }
        }

        public int Decode(uint vk, uint scanCode, Span<char> typed)
        {
            if (vk == DeadCircumflex)
            {
                pending = true;
                return -1;
            }

            char ch = vk switch
            {
                Space => ' ',
                Exclamation => '!',
                >= 'A' and <= 'Z' => (char)('a' + (vk - 'A')),
                _ => '\0',
            };
            if (ch == '\0')
            {
                return 0;
            }

            if (!pending)
            {
                typed[0] = ch;
                return 1;
            }

            pending = false;
            if (!readsAccent)
            {
                typed[0] = ch;
                return 1;
            }

            switch (ch)
            {
                case ' ':
                    typed[0] = '^';
                    return 1;
                case 'e':
                    typed[0] = 'ê';
                    return 1;
                case 'u':
                    typed[0] = 'û';
                    return 1;
                default:
                    typed[0] = '^';
                    typed[1] = ch;
                    return 2;
            }
        }
    }
}
