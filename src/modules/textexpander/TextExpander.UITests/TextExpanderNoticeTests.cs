// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// The notice an expansion shows at the text cursor, such as "No email address found". The test process shows it, from
/// the engine's own sources, over the typing target, as the engine shows it over the app being typed into. It must
/// never take the focus: what's typed while it shows goes on into the app, and clicking it only dismisses it.
/// </summary>
[TestClass]
public sealed class TextExpanderNoticeTests : TextExpanderTestBase
{
    private const string Heading = "No email address found";
    private const string Message = "There's no email address in what you copied, so that spot was left empty.";
    private const ushort VirtualKeyHome = 0x24;

    /// <summary>
    /// The notice appears under the caret without taking the focus, screen readers can read it, keys typed while it
    /// shows reach the app, and the user's next key fades it.
    /// </summary>
    [TestMethod("TextExpander.Notice.TypingGoesOnIntoTheAppWhileItShows")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Notice")]
    public void TypingGoesOnIntoTheAppWhileItShows()
    {
        Target.Clear();
        Target.Focus();
        TextInput.Type("Sent to ");
        AssertTargetShows("Sent to ");
        Native.RECT caret = CaretOnScreen();

        IntPtr notice = ShowNotice(Heading, Message);
        AssertNamedForScreenReaders(notice, Heading);
        AssertFocusStaysInTheTarget("as the notice appeared");
        AssertNeverActivates(notice);
        AssertUnderTheCaret(notice, caret);

        Step("Typing while the notice shows");
        TextInput.Type("you@example.com");
        AssertTargetShows("Sent to you@example.com");
        AssertFocusStaysInTheTarget("after typing");

        Step("Reporting a key, as the engine's keyboard hook does");
        NoticeBubble.OnUserInput();
        AssertHidden(notice, timeoutMS: 3_000, "a key was reported");
        AssertFocusStaysInTheTarget("after the notice faded");
    }

    /// <summary>A click on the notice dismisses it, and neither moves the focus nor reaches the app under it.</summary>
    [TestMethod("TextExpander.Notice.ClickingItDismissesItWithoutTakingTheFocus")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Notice")]
    public void ClickingItDismissesItWithoutTakingTheFocus()
    {
        // The caret at the start of the only line: a click under the line that reached the box would move it to the end.
        Target.Clear();
        Target.Focus();
        TextInput.Type("Sent to you");
        TextInput.Press(VirtualKeyHome);

        IntPtr notice = ShowNotice(Heading, Message);
        Assert.IsTrue(Native.GetWindowRect(notice, out Native.RECT bounds));
        int x = bounds.Left + ((bounds.Right - bounds.Left) / 2);
        int y = bounds.Top + ((bounds.Bottom - bounds.Top) / 2);
        Step($"Clicking the notice at ({x}, {y})");
        MouseHelper.LeftClickAt(x, y);

        AssertHidden(notice, timeoutMS: 3_000, "it was clicked");
        AssertFocusStaysInTheTarget("after the notice was clicked");

        Step("Typing where the caret was");
        TextInput.Type("Mail: ");
        AssertTargetShows("Mail: Sent to you");
    }

    /// <summary>One notice shows at a time, a new one replacing it, and it fades on its own after about 4 seconds.</summary>
    [TestMethod("TextExpander.Notice.OneShowsAtATimeAndFadesOnItsOwn")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Notice")]
    public void OneShowsAtATimeAndFadesOnItsOwn()
    {
        Target.Clear();
        Target.Focus();
        TextInput.Type("Call ");

        IntPtr first = ShowNotice(Heading, Message);
        IntPtr second = ShowNotice("No phone number found", "There's no phone number in what you copied, so that spot was left empty.");
        var clock = Stopwatch.StartNew();
        Assert.AreEqual(first, second, "The new notice takes the place of the one showing.");
        Assert.AreEqual(1, NoticeWindows().Count(Native.IsWindowVisible), "Only one notice shows.");
        AssertNamedForScreenReaders(second, "No phone number found");

        int lifetime = NoticeTiming.Lifetime(Native.SystemParametersInfoW(Native.SPI_GETMESSAGEDURATION, 0, out uint seconds, 0) ? seconds : 5);
        if (lifetime > NoticeTiming.LifetimeMs)
        {
            // Windows keeps notifications up longer on this machine; waiting minutes for that proves nothing more.
            Step($"Windows keeps notifications up {lifetime / 1_000} s here, so a key ends the notice instead");
            NoticeBubble.OnUserInput();
            AssertHidden(second, timeoutMS: 3_000, "a key was reported");
        }
        else
        {
            AssertHidden(second, timeoutMS: lifetime + 5_000, "nothing happened for a while");
            Assert.IsTrue(clock.ElapsedMilliseconds >= lifetime - 1_500, $"It faded after {clock.ElapsedMilliseconds} ms, too soon to read.");
        }

        AssertFocusStaysInTheTarget("after the notice faded");

        TextInput.Type("555-0100");
        AssertTargetShows("Call 555-0100");
    }

    [TestCleanup]
    public void DismissNotice() => NoticeBubble.Dismiss();

    private static IEnumerable<string> Strings(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => [element.GetString() ?? string.Empty],
        JsonValueKind.Object => element.EnumerateObject().SelectMany(property => Strings(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(Strings),
        _ => [],
    };

    /// <summary>Gets the notice windows of this process: the engine's sources make one, and keep it for every notice.</summary>
    private static List<IntPtr> NoticeWindows()
    {
        var found = new List<IntPtr>();
        var name = new char[64];
        Native.EnumWindows(
            (window, context) =>
            {
                int length = Native.GetClassNameW(window, name, name.Length);
                _ = Native.GetWindowThreadProcessId(window, out uint process);
                if (process == (uint)Environment.ProcessId && new string(name, 0, Math.Max(0, length)) == NoticeBubble.WindowClassName)
                {
                    found.Add(window);
                }

                return true;
            },
            IntPtr.Zero);
        return found;
    }

    /// <summary>Shows a notice for the target, and waits for it to be on the screen.</summary>
    private IntPtr ShowNotice(string heading, string message)
    {
        Step($"Showing the notice '{heading}' at the target's caret");
        NoticeBubble.Show(Target.Handle, heading, message);
        var shown = WaitHelper.WaitForStable(
            () => NoticeWindows().FirstOrDefault(Native.IsWindowVisible),
            window => window != IntPtr.Zero,
            timeoutMS: 5_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 50);
        Assert.IsTrue(shown.Succeeded, $"The notice didn't appear. Windows of its class here: {NoticeWindows().Count}.");
        return shown.LastObservation;
    }

    private void AssertFocusStaysInTheTarget(string when)
        => Assert.IsTrue(Target.HasKeyboardFocus, $"The typing target lost the keyboard focus {when}. Foreground: {WindowControl.GetForegroundWindowInfo()}.");

    /// <summary>A tool window that's never activated: no taskbar button, never in front, always over the app.</summary>
    private void AssertNeverActivates(IntPtr notice)
    {
        int style = Native.GetWindowLongW(notice, Native.GWL_EXSTYLE);
        Assert.AreNotEqual(0, style & Native.WS_EX_NOACTIVATE, "The notice can be activated.");
        Assert.AreNotEqual(0, style & Native.WS_EX_TOOLWINDOW, "The notice would have a taskbar button.");
        Assert.AreNotEqual(0, style & Native.WS_EX_TOPMOST, "The notice could go behind the app.");
        Assert.AreNotEqual(notice, Native.GetForegroundWindow(), "The notice is in front.");
        Step("The notice is a topmost tool window that's never activated");
    }

    /// <summary>Under the caret, or above it when there's no room, with the arrow's tip 2 pixels from it at 100%.</summary>
    private void AssertUnderTheCaret(IntPtr notice, Native.RECT caret)
    {
        Assert.IsTrue(Native.GetWindowRect(notice, out Native.RECT bounds));
        IntPtr monitor = Native.MonitorFromRect(ref caret, Native.MONITOR_DEFAULTTONEAREST);
        uint dpi = Native.GetDpiForMonitor(monitor, Native.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 ? dpiX : 96;
        NoticeLayout.Metrics metrics = NoticeLayout.Metrics.For(dpi, NoticeTheme.Current().HighContrast);
        Step($"Caret ({caret.Left}, {caret.Top})-({caret.Right}, {caret.Bottom}); notice ({bounds.Left}, {bounds.Top})-({bounds.Right}, {bounds.Bottom}) at {dpi} DPI");

        int caretX = caret.Left + ((caret.Right - caret.Left) / 2);
        Assert.IsTrue(bounds.Left + metrics.ShadowLeft <= caretX && caretX <= bounds.Right - metrics.ShadowRight, "The notice isn't across from the caret.");
        Assert.AreEqual((double)(caret.Bottom + metrics.Gap - metrics.ShadowTop), bounds.Top, 1.0, "The notice isn't just under the caret.");
    }

    /// <summary>
    /// Screen readers find the notice as a UI Automation element named with what it says. A notice that replaces one
    /// showing reuses its window, so the name is read until it's the new one.
    /// </summary>
    private void AssertNamedForScreenReaders(IntPtr notice, string heading)
    {
        Step("Reading the notice through UI Automation");
        string window = notice.ToInt64().ToString(CultureInfo.InvariantCulture);
        JsonElement tree = default;
        var named = WaitHelper.WaitForStable(
            () =>
            {
                tree = WinappCli.InvokeJson("ui", "inspect", "-w", window, "--json", "-d", "1");
                return Strings(tree).Any(text => text.Contains(heading, StringComparison.Ordinal));
            },
            found => found,
            timeoutMS: 3_000,
            pollIntervalMS: 100);
        Assert.IsTrue(named.Succeeded, $"No UI Automation name says '{heading}': {tree}");
    }

    private void AssertHidden(IntPtr notice, int timeoutMS, string after)
    {
        var hidden = WaitHelper.WaitForStable(() => Native.IsWindowVisible(notice), visible => !visible, timeoutMS, requiredConsecutiveMatches: 2, pollIntervalMS: 50);
        Assert.IsTrue(hidden.Succeeded, $"The notice still shows {timeoutMS} ms after {after}.");
        Step($"The notice went after {after}");
    }

    private void AssertTargetShows(string expected)
    {
        var result = WaitHelper.WaitForStable(() => Target.Text, text => text == expected, timeoutMS: 15_000, requiredConsecutiveMatches: 3, pollIntervalMS: 200);
        Assert.IsTrue(result.Succeeded, $"The target should show '{expected}', but shows '{result.LastObservation}'.");
        Step($"Target shows '{expected}'");
    }

    /// <summary>Gets the caret Windows keeps for the target's box, on the screen. The test process is per-monitor aware.</summary>
    private Native.RECT CaretOnScreen()
    {
        uint thread = Native.GetWindowThreadProcessId(Target.Handle, out _);
        var info = new Native.GUITHREADINFO { CbSize = (uint)Marshal.SizeOf<Native.GUITHREADINFO>() };
        Assert.IsTrue(Native.GetGUIThreadInfo(thread, ref info) && info.HwndCaret != IntPtr.Zero, "The typing target has no caret.");
        var topLeft = new Native.POINT { X = info.RcCaret.Left, Y = info.RcCaret.Top };
        var bottomRight = new Native.POINT { X = info.RcCaret.Right, Y = info.RcCaret.Bottom };
        Assert.IsTrue(Native.ClientToScreen(info.HwndCaret, ref topLeft) && Native.ClientToScreen(info.HwndCaret, ref bottomRight));
        return new Native.RECT { Left = topLeft.X, Top = topLeft.Y, Right = bottomRight.X, Bottom = bottomRight.Y };
    }
}
