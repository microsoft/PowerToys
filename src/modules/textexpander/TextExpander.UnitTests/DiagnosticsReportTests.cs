// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using System.Xml.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

/// <summary>The report <c>--diagnostics</c> shows in a message box, as opposed to the file <c>--diagnostics-file</c> writes.</summary>
[TestClass]
public sealed class DiagnosticsReportTests
{
    [TestMethod]
    public void TheReportReadsAsItDidInEnglish()
    {
        InjectionDiagnostics.Reset();
        try
        {
            InjectionDiagnostics.RecordClipboardOpen(ClipboardOpen.Owned);
            InjectionDiagnostics.RecordOutcome(PasteOutcome.Pasted);
            InjectionDiagnostics.RecordOutcome(PasteOutcome.NotAttempted);
            InjectionDiagnostics.RecordTypingSucceeded();
            InjectionDiagnostics.RecordClipboardRestoreSkipped();
            Assert.AreEqual(
                Lines(
                    "Status   : OK — replacements are being pasted.",
                    "Backend  : Auto (paste at 5 chars or more)",
                    "Last     : NotAttempted, clipboard Owned",
                    string.Empty,
                    "Pasted   : 1",
                    "Typed    : 1  (completed)",
                    "Typing routes: too short 1, paste refused 0",
                    "Abandoned: 0  (focus moved or failed after erase; failed after erase 0)",
                    "Cut short: 0  (focus moved, or a key or click, mid-typing)",
                    "SendInput failures: 0",
                    string.Empty,
                    "Clipboard opens: 1 owned, 0 unowned, 0 failed",
                    "Clipboard restores lost: 0",
                    "Clipboard restores skipped: 1  (a newer copy was kept)"),
                InjectionDiagnostics.BuildReport(InjectionBackend.Auto, 5));

            InjectionDiagnostics.Reset();
            StringAssert.StartsWith(
                InjectionDiagnostics.BuildReport(InjectionBackend.Clipboard, 12),
                Lines(
                    "Status   : Unknown — no expansion has run yet.",
                    "Backend  : Clipboard (paste at 12 chars or more)",
                    "Last     : no expansion yet, clipboard never opened"));
            Assert.AreEqual("Typing, as configured — POWERTOYS_TEXT_EXPANDER_INJECTION_BACKEND is set to type.", InjectionDiagnostics.DescribeHealth(InjectionBackend.Typing));

            InjectionDiagnostics.RecordOutcome(PasteOutcome.NotAttempted);
            InjectionDiagnostics.RecordTypingSucceeded();
            Assert.AreEqual("Not yet exercised — every expansion so far was short enough to type. Expand a snippet of five or more characters and look again.", InjectionDiagnostics.DescribeHealth(InjectionBackend.Auto));

            InjectionDiagnostics.RecordClipboardOpen(ClipboardOpen.Unowned);
            InjectionDiagnostics.RecordOutcome(PasteOutcome.RefusedBeforeErase);
            Assert.AreEqual("PASTE PATH INACTIVE — the clipboard only ever opened unowned, so Windows declined cross-thread clipboard ownership and every write refused. Expansions still work, by typing, but the paste path is doing nothing. The clipboard work needs to move onto the message-pump thread that owns the window.", InjectionDiagnostics.DescribeHealth(InjectionBackend.Auto));

            InjectionDiagnostics.RecordClipboardOpen(ClipboardOpen.Owned);
            Assert.AreEqual("PASTE UNAVAILABLE — pastes are being attempted and refused. Check whether the target window is elevated, or whether the clipboard holds something that cannot be restored (an image from an app that publishes only a bitmap handle).", InjectionDiagnostics.DescribeHealth(InjectionBackend.Auto));
        }
        finally
        {
            InjectionDiagnostics.Reset();
        }
    }

    [TestMethod]
    public void EveryLineOfTheReportCanBeTranslated()
    {
        // The report goes straight into a message box, so its labels and explanations are resources too, with only
        // numbers and the backend's, outcome's and clipboard's stable names filled in.
        string? resw = FindEngineResources();
        if (resw is null)
        {
            Assert.Inconclusive("The engine's sources aren't next to the tests here.");
        }

        string[] values = [.. XDocument.Load(resw).Root!.Elements("data").Select(data => (string)data.Element("value")!)];
        Regex[] formats = [.. values.Select(value => new Regex("^" + Regex.Replace(Regex.Escape(value), @"\\\{\d+}", ".*") + "$"))];
        var described = new HashSet<InjectionHealth>();
        Action[] states =
        [
            () => { },
            () => InjectionDiagnostics.RecordOutcome(PasteOutcome.Pasted),
            () => InjectionDiagnostics.RecordOutcome(PasteOutcome.NotAttempted),
            () =>
            {
                InjectionDiagnostics.RecordClipboardOpen(ClipboardOpen.Unowned);
                InjectionDiagnostics.RecordOutcome(PasteOutcome.RefusedBeforeErase);
            },
            () => InjectionDiagnostics.RecordOutcome(PasteOutcome.FailedAfterErase),
        ];

        try
        {
            foreach (Action state in states)
            {
                InjectionDiagnostics.Reset();
                state();
                foreach (InjectionBackend backend in Enum.GetValues<InjectionBackend>())
                {
                    described.Add(InjectionDiagnostics.Assess(backend));
                    CollectionAssert.Contains(values, InjectionDiagnostics.DescribeHealth(backend));
                    foreach (string line in InjectionDiagnostics.BuildReport(backend, 5).Split(Environment.NewLine))
                    {
                        Assert.IsTrue(line.Length == 0 || formats.Any(format => format.IsMatch(line)), line);
                    }
                }
            }
        }
        finally
        {
            InjectionDiagnostics.Reset();
        }

        CollectionAssert.AreEquivalent(Enum.GetValues<InjectionHealth>(), described.ToArray());
    }

    private static string Lines(params string[] lines) => string.Concat(lines.Select(line => line + Environment.NewLine));

    private static string? FindEngineResources()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            string resw = Path.Combine(folder.FullName, "src", "modules", "textexpander", "TextExpander", "Strings", "en-us", "Resources.resw");
            if (File.Exists(resw))
            {
                return resw;
            }
        }

        return null;
    }
}
