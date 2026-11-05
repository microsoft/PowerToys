// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class InjectionPolicyTests
{
    [TestMethod]
    public void ClipboardRoutingMatchesBackendAndThreshold()
    {
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("Thank you!", 5, InjectionBackend.Auto));
        Assert.IsFalse(InjectionPolicy.ShouldUseClipboard("brb", 5, InjectionBackend.Auto));
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("12345", 5, InjectionBackend.Auto));
        Assert.IsFalse(InjectionPolicy.ShouldUseClipboard("1234", 5, InjectionBackend.Auto));
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("a\nb", 5, InjectionBackend.Auto));
        Assert.IsFalse(InjectionPolicy.ShouldUseClipboard("line\nbreak", 5, InjectionBackend.Typing));
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("brb", 5, InjectionBackend.Clipboard));

        // The space given back after a snippet doesn't count toward the length; the rest is unchanged.
        Assert.IsFalse(InjectionPolicy.ShouldUseClipboard("1234 ", 5, InjectionBackend.Auto, givenBack: 1));
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("12345 ", 5, InjectionBackend.Auto, givenBack: 1));
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("a\nb ", 5, InjectionBackend.Auto, givenBack: 1));
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("b ", 5, InjectionBackend.Clipboard, givenBack: 1));
        Assert.IsFalse(InjectionPolicy.ShouldUseClipboard("12345 ", 5, InjectionBackend.Typing, givenBack: 1));

        // Only a keystroke given back, after a cancelled question, is typed even with Always paste.
        Assert.IsFalse(InjectionPolicy.ShouldUseClipboard(" ", 5, InjectionBackend.Clipboard, givenBack: 1));
    }

    [TestMethod]
    public void WindowsTerminalGetsOneLineTypedWhateverItsLength()
    {
        // Terminal trims trailing whitespace from a one-line paste, so the space given back after a pasted snippet was lost.
        Assert.IsFalse(InjectionPolicy.ShouldUseClipboard("you@example.com ", 5, InjectionBackend.Auto, givenBack: 1, windowsTerminal: true));
        Assert.IsFalse(InjectionPolicy.ShouldUseClipboard(new string('x', 500), 5, InjectionBackend.Auto, windowsTerminal: true));

        // Lines still paste there, so Terminal's multi-line paste warning still guards them.
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("echo first-line\necho second-line ", 5, InjectionBackend.Auto, givenBack: 1, windowsTerminal: true));

        // The insertion method chosen in Settings still decides: Always paste pastes, and the space is lost; Always type types.
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("you@example.com ", 5, InjectionBackend.Clipboard, givenBack: 1, windowsTerminal: true));
        Assert.IsFalse(InjectionPolicy.ShouldUseClipboard("echo first-line\necho second-line", 5, InjectionBackend.Typing, windowsTerminal: true));

        // Anywhere else, nothing changes.
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("you@example.com ", 5, InjectionBackend.Auto, givenBack: 1, windowsTerminal: false));
    }

    [TestMethod]
    public void WindowsTerminalGetsTextFromACopyOrWithControlCharactersPasted()
    {
        // Terminal filters control characters out of a paste and brackets it, but acts on them when they're typed: a Ctrl+O
        // hidden in a copied link ran the line in bash. Losing the space given back is the lesser harm.
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("curl https://example.com/install ", 5, InjectionBackend.Auto, givenBack: 1, windowsTerminal: true, fromClipboard: true));
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("name\tvalue ", 5, InjectionBackend.Auto, givenBack: 1, windowsTerminal: true));
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("a\u000Fb", 5, InjectionBackend.Auto, windowsTerminal: true));
        Assert.IsTrue(InjectionPolicy.ShouldUseClipboard("a\rb", 5, InjectionBackend.Auto, windowsTerminal: true), "A lone carriage return is a line break to a terminal.");
        Assert.IsFalse(InjectionPolicy.ShouldUseClipboard("git status ", 5, InjectionBackend.Auto, givenBack: 1, windowsTerminal: true, fromClipboard: false));

        // Elsewhere, where a copy goes is still the length's call.
        Assert.IsFalse(InjectionPolicy.ShouldUseClipboard("a@b.c", 50, InjectionBackend.Auto, windowsTerminal: false, fromClipboard: true));

        // Always type still types: the control characters are left out of what's typed into a terminal instead.
        Assert.IsFalse(InjectionPolicy.ShouldUseClipboard("a\u000Fb", 5, InjectionBackend.Typing, windowsTerminal: true, fromClipboard: true));
    }

    [TestMethod]
    public void LinesAreNotTypedIntoATerminal()
    {
        Assert.IsTrue(InjectionPolicy.RefusesTypedLines("cd ~/project\nrm -rf build", terminal: true));
        Assert.IsFalse(InjectionPolicy.RefusesTypedLines("git status", terminal: true));
        Assert.IsFalse(InjectionPolicy.RefusesTypedLines("Best regards,\nYour name", terminal: false));
    }

    [TestMethod]
    public void TheConsoleIsKnownByItsWindowClass()
    {
        Assert.IsTrue(InjectionPolicy.IsConsoleClass("ConsoleWindowClass"));
        Assert.IsTrue(InjectionPolicy.IsConsoleClass("consolewindowclass"));
        Assert.IsFalse(InjectionPolicy.IsConsoleClass("CASCADIA_HOSTING_WINDOW_CLASS"));
        Assert.IsFalse(InjectionPolicy.IsConsoleClass("ConsoleWindowClass2"));
        Assert.IsFalse(InjectionPolicy.IsConsoleClass(null));
    }

    [TestMethod]
    public void EveryControlCharacterButTabsAndLineBreaksIsRemoved()
    {
        for (char ch = '\0'; ch <= '\u00A0'; ch++)
        {
            bool control = ch < '\u0020' || (ch >= '\u007F' && ch <= '\u009F');
            bool kept = !control || ch is '\t' or '\n' or '\r';
            Assert.AreEqual(!kept, ControlCharacters.IsRemoved(ch), $"U+{(int)ch:X4}");
            Assert.AreEqual(kept ? $"a{ch}b" : "ab", ControlCharacters.Remove($"a{ch}b"), $"U+{(int)ch:X4}");
            Assert.AreEqual(control, ControlCharacters.HasAny($"a{ch}b"), $"U+{(int)ch:X4}");
        }

        const string clean = "Line one\r\nLine two\twith a tab, and émoji 🙂";
        Assert.AreSame(clean, ControlCharacters.Remove(clean), "Nothing to remove, nothing copied.");
        Assert.IsFalse(ControlCharacters.HasRemoved(clean));
        Assert.AreEqual("abc", ControlCharacters.Remove("\u001Ba\u0000b\u009Fc\u007F"));
        Assert.AreEqual(string.Empty, ControlCharacters.Remove("\u0003\u0004"));
    }

    [TestMethod]
    public void WindowsTerminalIsKnownByItsWindowClass()
    {
        Assert.IsTrue(InjectionPolicy.IsWindowsTerminalClass("CASCADIA_HOSTING_WINDOW_CLASS"));
        Assert.IsTrue(InjectionPolicy.IsWindowsTerminalClass("cascadia_hosting_window_class"), "Window classes ignore case.");

        // The console window Windows Terminal replaced doesn't trim a paste.
        Assert.IsFalse(InjectionPolicy.IsWindowsTerminalClass("ConsoleWindowClass"));
        Assert.IsFalse(InjectionPolicy.IsWindowsTerminalClass("CASCADIA_HOSTING_WINDOW_CLASS_2"));
        Assert.IsFalse(InjectionPolicy.IsWindowsTerminalClass("Notepad"));
        Assert.IsFalse(InjectionPolicy.IsWindowsTerminalClass(string.Empty));
        Assert.IsFalse(InjectionPolicy.IsWindowsTerminalClass(null));
        Assert.IsFalse(InputTarget.IsWindowsTerminal(IntPtr.Zero));
    }

    [TestMethod]
    public void EnvironmentParsingAcceptsDocumentedValuesOnly()
    {
        Assert.AreEqual(InjectionBackend.Typing, InjectionPolicy.ParseBackend("type", InjectionBackend.Auto));
        Assert.AreEqual(InjectionBackend.Clipboard, InjectionPolicy.ParseBackend(" PASTE ", InjectionBackend.Auto));
        Assert.AreEqual(InjectionBackend.Auto, InjectionPolicy.ParseBackend("nonsense", InjectionBackend.Auto));
        Assert.AreEqual(12, InjectionPolicy.ParseThreshold("12", 5));
        Assert.AreEqual(5, InjectionPolicy.ParseThreshold("0", 5));
        Assert.IsTrue(InjectionPolicy.ParseFlag("yes", false));
        Assert.IsFalse(InjectionPolicy.ParseFlag("off", true));
        Assert.IsTrue(InjectionPolicy.ParseFlag("banana", true));
    }
}
