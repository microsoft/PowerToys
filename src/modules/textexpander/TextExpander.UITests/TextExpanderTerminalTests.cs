// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// Expansion in Windows Terminal, which trims trailing whitespace from a one-line paste. The test reads what the shell got
/// rather than Terminal's screen: cmd, in a Terminal window of the test's own, echoes the line into a file in a folder of
/// the test's own.
/// </summary>
[TestClass]
[SkipWhenElevated]
public sealed class TextExpanderTerminalTests : TextExpanderTestBase
{
    private const string TerminalWindowClass = "CASCADIA_HOSTING_WINDOW_CLASS";

    // Long enough to be pasted anywhere else.
    private static readonly Snippet Echo = new(";teecho", "echo spaced-out");

    internal override IReadOnlyList<Snippet>? InitialSnippets => [.. DefaultSnippets, Echo];

    /// <summary>
    /// A one-line snippet long enough to be pasted in other apps is typed in Windows Terminal, so the space that released
    /// the trigger stays after it.
    /// </summary>
    [TestMethod("TextExpander.Terminal.KeepsTheSpaceAfterAOneLineSnippet")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Terminal")]
    public void KeepsTheSpaceAfterAOneLineSnippet()
    {
        string terminal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
        if (!File.Exists(terminal))
        {
            Assert.Inconclusive("Windows Terminal isn't installed.");
        }

        // The engine is ready.
        AssertExpands(Greeting.Trigger + " ", Greeting.Replacement + " ", waitForEngine: true);

        string folder = Directory.CreateTempSubdirectory("TextExpanderUITest-").FullName;
        string title = "Text Expander UI test " + Guid.NewGuid().ToString("N")[..8];
        IntPtr window = IntPtr.Zero;
        try
        {
            Step($"Opening cmd in a new Windows Terminal window, in {folder}");
            using (Process.Start(new ProcessStartInfo(terminal)
            {
                ArgumentList = { "-w", "new", "new-tab", "--title", title, "--suppressApplicationTitle", "-d", folder, "cmd.exe", "/k", "echo ready>ready.txt" },
                UseShellExecute = false,
            }))
            {
            }

            var opened = WaitHelper.WaitForStable(
                () => WindowControl.EnumerateAllWindows().FirstOrDefault(found => found.ClassName == TerminalWindowClass && found.Title == title).Hwnd,
                found => found != IntPtr.Zero,
                timeoutMS: 30_000,
                pollIntervalMS: 250);
            Assert.IsTrue(opened.Succeeded, "The Windows Terminal window didn't open.");
            window = opened.LastObservation;
            Assert.IsTrue(
                WaitHelper.WaitForStable(() => File.Exists(Path.Combine(folder, "ready.txt")), ready => ready, timeoutMS: 30_000, pollIntervalMS: 250).Succeeded,
                "cmd didn't start in Windows Terminal.");
            Assert.IsTrue(
                WindowControl.WaitForForeground(window, timeoutMS: 10_000, requiredConsecutiveMatches: 3),
                $"Windows Terminal didn't come in front. Foreground: {WindowControl.GetForegroundWindowInfo()}.");

            // The engine checks each new window in front before it reads keys there.
            Thread.Sleep(1_000);
            Step($"Typing '{Echo.Trigger} ' into cmd");
            TextInput.Type(Echo.Trigger + " ");

            // Keys typed while the snippet goes in are held and land after it, but not ones typed before it starts.
            Thread.Sleep(3_000);
            Step("Typing 'x>out.txt' and Enter, so cmd writes what the line says");
            TextInput.Type("x>out.txt");
            TextInput.Enter();

            string output = Path.Combine(folder, "out.txt");
            var written = WaitHelper.WaitForStable(
                () => File.Exists(output) ? File.ReadAllText(output).TrimEnd('\r', '\n') : null,
                text => text is { Length: > 0 },
                timeoutMS: 15_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 250,
                shouldRetryException: ex => ex is IOException);
            Assert.AreEqual(
                "spaced-out x",
                written.LastObservation,
                $"cmd got '{TextExpanderFiles.Visible(written.LastObservation)}'. Without the space given back, it says 'spaced-outx'. Engine: {DescribeEngine()}; {EngineDiagnostics.Read()}.");
            Step("cmd got the snippet with the space after it");
        }
        finally
        {
            if (window != IntPtr.Zero)
            {
                if (WindowControl.GetForegroundWindowHandle() == window)
                {
                    TextInput.Type("exit");
                    TextInput.Enter();
                }

                if (!WindowControl.TryCloseWindow(window.ToInt64(), 10_000))
                {
                    Step("The Windows Terminal window is still open");
                }
            }

            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // cmd may still be closing in it; it's a temporary folder.
            }
        }
    }
}
