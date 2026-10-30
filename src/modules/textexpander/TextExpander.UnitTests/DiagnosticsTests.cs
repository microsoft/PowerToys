// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class DiagnosticsTests
{
    [TestMethod]
    public void DiagnosticsReportHealthyAndFailureStates()
    {
        InjectionDiagnostics.Reset();
        Assert.AreEqual(InjectionHealth.Unknown, InjectionDiagnostics.Assess(InjectionBackend.Auto));

        InjectionDiagnostics.RecordOutcome(PasteOutcome.NotAttempted);
        InjectionDiagnostics.RecordTypingSucceeded();
        Assert.AreEqual(InjectionHealth.Untested, InjectionDiagnostics.Assess(InjectionBackend.Auto));

        InjectionDiagnostics.Reset();
        InjectionDiagnostics.RecordClipboardOpen(ClipboardOpen.Owned);
        InjectionDiagnostics.RecordOutcome(PasteOutcome.Pasted);
        Assert.AreEqual(InjectionHealth.Pasting, InjectionDiagnostics.Assess(InjectionBackend.Auto));
        StringAssert.Contains(InjectionDiagnostics.BuildMachineReport(InjectionBackend.Auto, 5), "pasted=1\n");

        InjectionDiagnostics.Reset();
        InjectionDiagnostics.RecordClipboardOpen(ClipboardOpen.Unowned);
        InjectionDiagnostics.RecordOutcome(PasteOutcome.RefusedBeforeErase);
        InjectionDiagnostics.RecordTypingSucceeded();
        Assert.AreEqual(InjectionHealth.ClipboardOwnershipDeclined, InjectionDiagnostics.Assess(InjectionBackend.Auto));
        InjectionDiagnostics.RecordClipboardRestoreSkipped();
        InjectionDiagnostics.RecordTypingAbandoned();
        string report = InjectionDiagnostics.BuildMachineReport(InjectionBackend.Auto, 5);
        StringAssert.Contains(report, "restores_skipped=1\n");
        StringAssert.Contains(report, "typing_abandoned=1\n");
        InjectionDiagnostics.Reset();
    }

    [TestMethod]
    public void DiagnosticsCountsSkippedRestoresAndAbandonedTyping()
    {
        InjectionDiagnostics.Reset();
        InjectionDiagnostics.RecordClipboardRestoreSkipped();
        InjectionDiagnostics.RecordTypingAbandoned();
        Assert.AreEqual(1, InjectionDiagnostics.ClipboardRestoresSkipped);
        Assert.AreEqual(0, InjectionDiagnostics.ClipboardRestoreFailures);
        Assert.AreEqual(1, InjectionDiagnostics.TypingAbandoned);
        InjectionDiagnostics.RecordSendInputFailure();
        Assert.AreEqual(1, InjectionDiagnostics.SendInputFailures);
        string report = InjectionDiagnostics.BuildMachineReport(InjectionBackend.Auto, 5);
        StringAssert.Contains(report, "restores_skipped=1\n");
        StringAssert.Contains(report, "typing_abandoned=1\n");
        StringAssert.Contains(report, "send_input_failures=1\n");
        InjectionDiagnostics.Reset();

        InjectionDiagnostics.RecordOutcome(PasteOutcome.FailedAfterErase);
        Assert.AreEqual(0, InjectionDiagnostics.Typed);
        Assert.AreEqual(1, InjectionDiagnostics.Abandoned);
        Assert.AreEqual(InjectionHealth.PasteUnavailable, InjectionDiagnostics.Assess(InjectionBackend.Auto));
        StringAssert.Contains(InjectionDiagnostics.BuildMachineReport(InjectionBackend.Auto, 5), "failed_after_erase=1\n");
        InjectionDiagnostics.Reset();
    }

    [TestMethod]
    public void TheDiagnosticsFileNeverNamesTheSnippetsFile()
    {
        // Bug reports copy the file as it is.
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string snippets = Path.Combine(scratch.Path, "Contoso merger", "Contoso.json");
        Directory.CreateDirectory(Path.GetDirectoryName(snippets)!);
        File.WriteAllText(snippets, SnippetJson.Of("!a=A"));
        string diagnostics = Path.Combine(scratch.Path, "diagnostics.txt");
        using var store = new SnippetStore(snippets);
        store.Load();

        Program.WriteDiagnosticsFile(store, diagnostics);
        string text = File.ReadAllText(diagnostics);
        StringAssert.StartsWith(text, "snippets_location=custom\nsnippets_file_exists=true\nsnippet_count=1\n");
        Assert.IsFalse(text.Contains("Contoso", StringComparison.OrdinalIgnoreCase), text);

        File.Delete(snippets);
        Program.WriteDiagnosticsFile(store, diagnostics);
        StringAssert.Contains(File.ReadAllText(diagnostics), "snippets_file_exists=false\n");

        using var defaultStore = new SnippetStore(Path.Combine(scratch.Path, SnippetStore.FileName), isDefault: true);
        Program.WriteDiagnosticsFile(defaultStore, diagnostics);
        StringAssert.StartsWith(File.ReadAllText(diagnostics), "snippets_location=default\n");
    }
}
