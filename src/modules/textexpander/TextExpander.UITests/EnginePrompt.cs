// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// The window the engine opens for a <c>{{prompt:…}}</c> question: "PowerToys Text Expander", the question over the
/// Answer box, then OK and Cancel. It belongs to PowerToys.TextExpander.exe.
/// </summary>
internal sealed class EnginePrompt
{
    public const string Title = "PowerToys Text Expander";

    private EnginePrompt(Session session)
    {
        Session = session;
        Handle = new IntPtr(session.WindowHandle);
    }

    public Session Session { get; }

    public IntPtr Handle { get; }

    public bool IsOpen => IsWindow(Handle) && IsWindowVisible(Handle);

    /// <summary>Gets the Answer box's name, which is the snippet's question.</summary>
    public string Question => Answer.Name;

    public bool OkEnabled => Session.Find<Button>(By.AccessibilityId("OkButton"), 5_000).IsEnabled;

    public bool ErrorShown => Session.FindExact<Element>("Enter an answer to continue.").Count > 0;

    public string AnswerText => Answer.Value;

    private TextBox Answer => Session.Find<TextBox>(By.AccessibilityId("Answer"), 5_000);

    /// <summary>Waits for the engine's question window to open.</summary>
    public static EnginePrompt? WaitForOpen(int timeoutMS = 15_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMS);
        do
        {
            foreach (var processId in TextExpanderTestBase.ProcessIds(TextExpanderTestBase.EngineProcessName))
            {
                // The engine's message boxes share the caption; the question window is the WinUI one.
                var window = WindowsFinder.ListByApp(processId.ToString(CultureInfo.InvariantCulture))
                    .FirstOrDefault(candidate => candidate.Title == Title && candidate.ClassName == "WinUIDesktopWin32WindowClass");
                if (window is not null)
                {
                    var session = WindowsFinder.WaitForWindowByApp(
                        processId.ToString(CultureInfo.InvariantCulture),
                        candidate => candidate.Hwnd == window.Hwnd,
                        timeoutMS: 1_000);
                    if (session is not null)
                    {
                        return new EnginePrompt(session);
                    }
                }
            }

            Thread.Sleep(100);
        }
        while (DateTime.UtcNow < deadline);

        return null;
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

    // A UI Automation read can take seconds on one CPU.
    public bool WaitForOkEnabled(bool enabled, int timeoutMS = 10_000) =>
        Session.WaitFor(() => OkEnabled == enabled, timeoutMS, pollIntervalMS: 200);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);
}
