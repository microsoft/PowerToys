// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
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

    /// <summary>Picks <paramref name="text"/> with a click, as a mouse user does.</summary>
    public void Pick(string text) => Session.WaitForExact<Element>(text, 5_000).Click(msPostAction: 0);

    /// <summary>Whether the window comes in front by itself, as it does over the app being typed in.</summary>
    public bool WaitForForeground(int timeoutMS = 10_000) =>
        WaitHelper.WaitForStable(
            WindowControl.GetForegroundWindowHandle,
            foreground => foreground == Handle,
            timeoutMS,
            requiredConsecutiveMatches: 2).Succeeded;

    public bool WaitForClosed(int timeoutMS = 10_000) =>
        WaitHelper.WaitForStable(() => IsOpen, open => !open, timeoutMS, requiredConsecutiveMatches: 2).Succeeded;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);
}
