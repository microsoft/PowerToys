// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

/// <summary>The notice's timing, colors and what screen readers hear, without a window.</summary>
[TestClass]
public sealed class NoticeBubbleTests
{
    private const string Heading = "No email address found";
    private const string Message = "There's no email address in what you copied, so that spot was left empty.";

    [TestMethod]
    public void StaysUpAboutFourSecondsOrAsLongAsWindowsKeepsNotificationsUp()
    {
        Assert.AreEqual(4_000, NoticeTiming.Lifetime(5), "Windows' default of 5 seconds for its own notifications.");
        Assert.AreEqual(4_000, NoticeTiming.Lifetime(0));
        Assert.AreEqual(7_000, NoticeTiming.Lifetime(7), "Someone asked Windows to keep notifications up longer.");
        Assert.AreEqual(300_000, NoticeTiming.Lifetime(300));
        Assert.AreEqual(300_000, NoticeTiming.Lifetime(100_000), "Capped at Windows' longest choice, 5 minutes.");
    }

    [TestMethod]
    public void ShowsOnlyWhileItsAppIsStillInFront()
    {
        // The user went to another app while the caret was found, and the notice came up over it, at the old app's caret.
        IntPtr app = new(0x10);
        IntPtr other = new(0x20);
        static IntPtr Root(IntPtr window) => window;

        Assert.IsTrue(NoticeBubble.IsForTheAppInFront(app, app, Root));
        Assert.IsFalse(NoticeBubble.IsForTheAppInFront(app, other, Root), "Another app came to the front.");
        Assert.IsTrue(NoticeBubble.IsForTheAppInFront(app, IntPtr.Zero, Root), "Windows is between two windows.");
        Assert.IsTrue(NoticeBubble.IsForTheAppInFront(IntPtr.Zero, other, Root), "No app was given.");
    }

    [TestMethod]
    public void AKeyOrClickFadesItOnceItHasBeenUpAMoment()
    {
        Assert.AreEqual(0, NoticeTiming.FadeDelay(shownAtMs: 10_000, nowMs: 12_000), "Up 2 seconds: it fades at once.");
        Assert.AreEqual(0, NoticeTiming.FadeDelay(10_000, 11_000), "Up exactly a second.");
        Assert.AreEqual(800, NoticeTiming.FadeDelay(10_000, 10_200), "A key typed just as it appeared: it stays up a second in all.");
        Assert.AreEqual(1_000, NoticeTiming.FadeDelay(10_000, 10_000));
        Assert.AreEqual(1_000, NoticeTiming.FadeDelay(10_000, 9_000), "A clock that went back doesn't keep it up longer.");
    }

    [TestMethod]
    public void ScreenReadersAreToldOnceTheyCanBeWhileItsStillUp()
    {
        // Told as it appears, a new window's first notice reached no screen reader: none had connected to the window yet.
        Assert.IsTrue(NoticeTiming.AnnounceDelayMs >= 100, "Screen readers connect to the window as it appears.");
        Assert.IsTrue(NoticeTiming.AnnounceDelayMs < NoticeTiming.MinimumVisibleMs, "No key or click fades it before it's told.");
        Assert.IsTrue(NoticeTiming.AnnounceDelayMs < NoticeTiming.LifetimeMs);
    }

    [TestMethod]
    public void FadesInAndOutSmoothly()
    {
        Assert.AreEqual(0, NoticeTiming.Opacity(0, NoticeTiming.FadeInMs, 0, 255));
        Assert.AreEqual(128, NoticeTiming.Opacity(50, 100, 0, 255));
        Assert.AreEqual(255, NoticeTiming.Opacity(100, 100, 0, 255));
        Assert.AreEqual(255, NoticeTiming.Opacity(140, 100, 0, 255), "A late frame lands on the end.");

        Assert.AreEqual(255, NoticeTiming.Opacity(0, NoticeTiming.FadeOutMs, 255, 0));
        Assert.AreEqual(128, NoticeTiming.Opacity(125, 250, 255, 0));
        Assert.AreEqual(0, NoticeTiming.Opacity(250, 250, 255, 0));
        Assert.AreEqual(64, NoticeTiming.Opacity(125, 250, 128, 0), "A fade that starts part way in starts from there.");
    }

    [TestMethod]
    public void FollowsTheLightAndDarkThemesAndHighContrastsOwnColors()
    {
        Assert.AreEqual(NoticeTheme.LightPalette, NoticeTheme.Choose(highContrast: false, appsUseLightTheme: true, 0x000000, 0xFFFFFF));
        Assert.AreEqual(NoticeTheme.DarkPalette, NoticeTheme.Choose(highContrast: false, appsUseLightTheme: false, 0x000000, 0xFFFFFF));

        // High contrast takes the theme's colors whatever the app mode, and the icon is the text's color, so nothing
        // relies on a color of our own.
        NoticeTheme.Palette contrast = NoticeTheme.Choose(highContrast: true, appsUseLightTheme: false, windowColor: 0x000000, windowTextColor: 0xFFFF00);
        Assert.AreEqual(new NoticeTheme.Palette(Fill: 0x000000, Text: 0xFFFF00, Border: 0xFFFF00, Icon: 0xFFFF00, HighContrast: true, Dark: false), contrast);
        Assert.AreEqual(0, NoticePainter.Shadows(96, contrast).Length, "High contrast has no shadow.");

        Assert.AreEqual(2, NoticePainter.Shadows(96, NoticeTheme.LightPalette).Length, "A flyout's two shadows.");
        Assert.IsTrue(NoticePainter.Shadows(96, NoticeTheme.DarkPalette)[0].Opacity > NoticePainter.Shadows(96, NoticeTheme.LightPalette)[0].Opacity, "Darker in the dark theme.");
        Assert.AreEqual(12f, NoticePainter.Shadows(144, NoticeTheme.LightPalette)[0].OffsetY, "8 pixels down at 100% are 12 at 150%.");

        Assert.AreEqual(0x112233u, NoticeTheme.SwapRedAndBlue(0x00332211), "Windows gives colors with red in the low byte.");
    }

    [TestMethod]
    public void ScreenReadersHearTheHeadingThenTheMessage()
    {
        Assert.AreEqual(Heading + ". " + Message, NoticeAutomation.SpokenText(Heading, Message, "{0}. {1}"));
        Assert.AreEqual("見つかりません。" + Message, NoticeAutomation.SpokenText("見つかりません", Message, "{0}。{1}"), "The punctuation comes from the translation.");
        Assert.AreEqual(Message, NoticeAutomation.SpokenText("  ", Message, "{0}. {1}"), "No heading.");
        Assert.AreEqual(Heading, NoticeAutomation.SpokenText(Heading + " ", string.Empty, "{0}. {1}"), "No message.");
        Assert.AreEqual(Heading + ". " + Message, NoticeAutomation.SpokenText(Heading, Message, "{0}. {2}"), "A broken translation falls back to English punctuation.");
        Assert.AreEqual(Heading + ". " + Message, NoticeAutomation.SpokenText(Heading, Message), "English, when the resources can't be read.");
    }

    [TestMethod]
    public void IsAToolTipNamedWithWhatItSaysThatNeverTakesTheFocus()
    {
        string spoken = NoticeAutomation.SpokenText(Heading, Message, "{0}. {1}");
        Assert.AreEqual(Native.UIA_ToolTipControlTypeId, NoticeAutomation.Property(Native.UIA_ControlTypePropertyId, spoken));
        Assert.AreEqual(spoken, NoticeAutomation.Property(Native.UIA_NamePropertyId, spoken));
        Assert.AreEqual("TextExpanderNotice", NoticeAutomation.Property(Native.UIA_AutomationIdPropertyId, spoken));
        Assert.AreEqual(false, NoticeAutomation.Property(Native.UIA_IsKeyboardFocusablePropertyId, spoken));
        Assert.AreEqual(false, NoticeAutomation.Property(Native.UIA_HasKeyboardFocusPropertyId, spoken));
        Assert.IsNull(NoticeAutomation.Property(30001, spoken), "Its bounds come from the window.");
    }

    [TestMethod]
    public void ItsWindowPatternClosesItAndSaysItCannotBeResized()
    {
        int closed = 0;
        var automation = new NoticeAutomation(IntPtr.Zero, () => closed++);
        IRawElementProviderSimple simple = automation;
        IWindowProvider window = automation;

        Assert.AreEqual(Native.S_OK, simple.GetProviderOptions(out int options));
        Assert.AreEqual(Native.ProviderOptions_ServerSideProvider, options);

        Assert.AreEqual(Native.S_OK, simple.GetPatternProvider(Native.UIA_WindowPatternId, out IntPtr pattern));
        Assert.AreNotEqual(IntPtr.Zero, pattern, "A popup needs the Window pattern for its notification to reach screen readers.");
        _ = System.Runtime.InteropServices.Marshal.Release(pattern);
        Assert.AreEqual(Native.S_OK, simple.GetPatternProvider(10000, out IntPtr invoke));
        Assert.AreEqual(IntPtr.Zero, invoke, "It can't be invoked.");

        Assert.AreEqual(Native.S_OK, window.Close());
        Assert.AreEqual(1, closed, "Closing it through UI Automation dismisses it.");
        Assert.AreEqual(Native.S_OK, window.GetCanMaximize(out int canMaximize));
        Assert.AreEqual(0, canMaximize);
        Assert.AreEqual(Native.S_OK, window.GetCanMinimize(out int canMinimize));
        Assert.AreEqual(0, canMinimize);
        Assert.AreEqual(Native.S_OK, window.GetIsModal(out int modal));
        Assert.AreEqual(0, modal);
        Assert.AreEqual(Native.S_OK, window.GetIsTopmost(out int topmost));
        Assert.AreEqual(1, topmost);
        Assert.AreEqual(Native.UIA_E_INVALIDOPERATION, window.SetVisualState(1), "It can't be maximized.");
    }

    [TestMethod]
    public void TheLatestShowOrDismissWinsWhateverOrderTheirMessagesArriveIn()
    {
        var requests = new NoticeRequests();
        Assert.AreEqual(0, requests.Latest.Generation, "Nothing asked yet.");

        var email = new NoticeRequests.Notice(IntPtr.Zero, Heading, Message);
        long shown = requests.Set(email);
        Assert.AreSame(email, requests.Latest.Notice);
        Assert.IsFalse(requests.Overtaken(shown));

        // Dismissed while the notice's thread was finding the caret for it: it isn't shown then.
        long dismissed = requests.Set(null);
        Assert.IsTrue(requests.Overtaken(shown));
        Assert.IsNull(requests.Latest.Notice);

        // Shown again after that: whichever of their messages comes first, the latest ask is this notice.
        var phone = new NoticeRequests.Notice(IntPtr.Zero, "No phone number found", Message);
        long replaced = requests.Set(phone);
        Assert.IsTrue(replaced > dismissed);
        Assert.IsTrue(requests.Overtaken(dismissed));
        Assert.AreSame(phone, requests.Latest.Notice);
    }

    [TestMethod]
    public void ShowsAndDismissesFromManyThreadsEachGetTheirOwnPlace()
    {
        var requests = new NoticeRequests();
        var generations = new System.Collections.Concurrent.ConcurrentBag<long>();
        Parallel.For(0, 1_000, i => generations.Add(requests.Set(i % 3 == 0 ? null : new NoticeRequests.Notice(new IntPtr(i), Heading, Message))));

        CollectionAssert.AreEquivalent(Enumerable.Range(1, 1_000).Select(i => (long)i).ToArray(), generations.ToArray());
        Assert.AreEqual(1_000, requests.Latest.Generation);
    }

    [TestMethod]
    public void ShowingNothingShowsNothingAndInputWithNoNoticeIsIgnored()
    {
        // The hooks call OnUserInput for every key and click, with or without a notice.
        NoticeBubble.Show(IntPtr.Zero, " ", string.Empty);
        NoticeBubble.OnUserInput();
        NoticeBubble.Dismiss();
        Assert.IsFalse(NoticeWindowExistsHere(), "Nothing to say starts no window.");
    }

    private static bool NoticeWindowExistsHere()
    {
        bool found = false;
        var name = new char[64];
        Native.EnumWindows(
            (window, context) =>
            {
                int length = Native.GetClassNameW(window, name, name.Length);
                _ = Native.GetWindowThreadProcessId(window, out uint process);
                found = process == (uint)Environment.ProcessId && new string(name, 0, Math.Max(0, length)) == NoticeBubble.WindowClassName;
                return !found;
            },
            IntPtr.Zero);
        return found;
    }
}
