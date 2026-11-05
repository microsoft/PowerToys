// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// The window the engine opens to pick one of a clipboard variable's matches, as for <c>{{clipboard:link}}</c> with two links:
/// "PowerToys Text Expander", the question over the list of matches (<c>Options</c>), then Insert and Cancel. It belongs
/// to PowerToys.TextExpander.exe and shares the question window's caption and class, so it's told apart by its list.
/// </summary>
internal sealed class EngineChoice
{
    private const string ClickCatcherClass = "Shell_LightDismissOverlay";

    private const uint GaRoot = 2;

    private EngineChoice(Session session)
    {
        Session = session;
        Handle = new IntPtr(session.WindowHandle);
    }

    public Session Session { get; }

    public IntPtr Handle { get; }

    public bool IsOpen => IsWindow(Handle) && IsWindowVisible(Handle);

    /// <summary>Waits for the engine's choice window to open.</summary>
    public static EngineChoice? WaitForOpen(int timeoutMS = 15_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMS);
        do
        {
            foreach (var processId in TextExpanderTestBase.ProcessIds(TextExpanderTestBase.EngineProcessName))
            {
                var window = WindowsFinder.ListByApp(processId.ToString(CultureInfo.InvariantCulture))
                    .FirstOrDefault(candidate => candidate.Title == EnginePrompt.Title && candidate.ClassName == "WinUIDesktopWin32WindowClass");
                if (window is null)
                {
                    continue;
                }

                var session = WindowsFinder.WaitForWindowByApp(
                    processId.ToString(CultureInfo.InvariantCulture),
                    candidate => candidate.Hwnd == window.Hwnd,
                    timeoutMS: 1_000);
                if (session is not null && session.Has(By.AccessibilityId("Options"), timeoutMS: 2_000))
                {
                    return new EngineChoice(session);
                }
            }

            Thread.Sleep(100);
        }
        while (DateTime.UtcNow < deadline);

        return null;
    }

    /// <summary>Whether the list offers <paramref name="text"/>.</summary>
    public bool Offers(string text) => Session.WaitForExactName(text, 5_000);

    /// <summary>
    /// Picks <paramref name="text"/> with a click, as a mouse user does. A shell flyout's click catcher
    /// (<c>Shell_LightDismissOverlay</c>), which Windows 10 test agents had over part of the screen, takes a click to close
    /// itself and passes nothing on, so it's clicked away first, as a user would. Anything else over the match fails.
    /// </summary>
    public void Pick(string text, Action<string>? step = null)
    {
        var option = Session.WaitForExact<Element>(text, 5_000);
        int x = option.X + (option.Width / 2);
        int y = option.Y + (option.Height / 2);
        if (!WindowControl.IsPointOwnedByWindow(Handle, x, y))
        {
            string covering = TypingTarget.DescribeWindowAt(Handle, x, y);
            Assert.AreEqual(ClickCatcherClass, ClassAt(x, y), $"Something is over the match '{text}': {covering}.");
            step?.Invoke($"A shell flyout's click catcher is over the match ({covering}); clicking it away first");
            MouseHelper.LeftClickAt(x, y);
            var cleared = WaitHelper.WaitForStable(() => !IsOpen || WindowControl.IsPointOwnedByWindow(Handle, x, y), done => done, timeoutMS: 3_000);
            Assert.IsTrue(cleared.Succeeded, $"The click catcher stayed over the match: {TypingTarget.DescribeWindowAt(Handle, x, y)}.");
            if (!IsOpen)
            {
                return;
            }
        }

        option.Click(msPostAction: 0);
    }

    /// <summary>Whether the window comes in front by itself, as it does over the app being typed in.</summary>
    public bool WaitForForeground(int timeoutMS = 10_000) =>
        WaitHelper.WaitForStable(
            WindowControl.GetForegroundWindowHandle,
            foreground => foreground == Handle,
            timeoutMS,
            requiredConsecutiveMatches: 2).Succeeded;

    public bool WaitForClosed(int timeoutMS = 10_000) =>
        WaitHelper.WaitForStable(() => IsOpen, open => !open, timeoutMS, requiredConsecutiveMatches: 2).Succeeded;

    /// <summary>The class of the top-level window at a screen point.</summary>
    private static string ClassAt(int x, int y)
    {
        IntPtr at = WindowFromPoint(new Point { X = x, Y = y });
        IntPtr root = at == IntPtr.Zero ? IntPtr.Zero : GetAncestor(at, GaRoot);
        var name = new StringBuilder(256);
        return root != IntPtr.Zero && GetClassNameW(root, name, name.Capacity) > 0 ? name.ToString() : string.Empty;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr window, StringBuilder name, int capacity);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);
}
