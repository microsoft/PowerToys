// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class FileAccessWorkflowTests
{
    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task SelectedReadOnlyGrantDoesNotPermitWritesOrSiblingReads()
    {
        using var source = new FileAccessTestDirectory();
        using var first = new RunSession();
        using var second = new RunSession();
        var file = Path.Combine(source.WorkingDirectory, "private.txt");
        var sibling = Path.Combine(source.WorkingDirectory, "neighbor.txt");
        File.WriteAllText(file, "permitted-marker");
        File.WriteAllText(sibling, "neighbor-secret");
        var script = $"try {{ Get-Content -LiteralPath '{file}' -ErrorAction Stop }} catch {{ 'READ_BLOCKED'; $_.Exception.Message }}; try {{ Set-Content -LiteralPath '{file}' -Value 'changed' -ErrorAction Stop; 'UNEXPECTED_WRITE' }} catch {{ 'WRITE_BLOCKED' }}; try {{ Get-Content -LiteralPath '{sibling}' -ErrorAction Stop }} catch {{ 'NEIGHBOR_BLOCKED' }}";
        var request = FileReadGrantTests.Request(first) with { Script = script };
        IsolationReport? report = null;
        RunEnvironment? environment = null;
        var before = new OutputBuffer();
        var progress = new MultiBackendTests.ImmediateProgress(message =>
        {
            if (message.Kind == WorkerMessage.Output)
            {
                before.Append(message.Text);
            }

            report = message.Report ?? report;
            environment = message.Environment ?? environment;
        });
        await new WorkerClient(MultiBackendTests.Worker()).RunAsync(request, progress, CancellationToken.None);
        StringAssert.Contains(before.ToString(), "READ_BLOCKED");
        Assert.IsNotNull(report);
        PolicySettings retryPolicy;
        using (var grant = FileReadGrant.CreateSelectedFile(file, request, environment!))
        {
            retryPolicy = grant.Policy;
        }

        // Release the host lease: the following write denial must come from MXC.
        var after = await MultiBackendTests.Execute(request with { WorkingDirectory = second.WorkingDirectory, TemporaryDirectory = second.TemporaryDirectory, Policy = retryPolicy });
        StringAssert.Contains(after, "permitted-marker");
        StringAssert.Contains(after, "WRITE_BLOCKED");
        StringAssert.Contains(after, "NEIGHBOR_BLOCKED");
        Assert.IsFalse(after.Contains("UNEXPECTED_WRITE", StringComparison.Ordinal));
        Assert.IsFalse(after.Contains("neighbor-secret", StringComparison.Ordinal));
        Assert.AreEqual("permitted-marker", File.ReadAllText(file));
        Assert.AreEqual("neighbor-secret", File.ReadAllText(sibling));
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    [DataRow(WorkloadKind.WindowsApplication)]
    [DataRow(WorkloadKind.WindowsPowerShell)]
    [DataRow(WorkloadKind.WindowsBatch)]
    public async Task ReviewCancelAndRetryUseTheRecordedWindowsTask(WorkloadKind kind)
    {
        using var source = new FileAccessTestDirectory();
        var file = Path.Combine(source.WorkingDirectory, "private.txt");
        File.WriteAllText(file, "retry-marker\n");
        var input = kind == WorkloadKind.WindowsApplication ? Path.Combine(Environment.SystemDirectory, "sort.exe") : Path.Combine(source.WorkingDirectory, kind == WorkloadKind.WindowsPowerShell ? "read.ps1" : "read.cmd");
        if (kind == WorkloadKind.WindowsPowerShell)
        {
            File.WriteAllText(input, $"Set-Content prior-result.txt 'previous result'; Get-Content -LiteralPath '{file}' -ErrorAction Stop");
        }
        else if (kind == WorkloadKind.WindowsBatch)
        {
            File.WriteAllText(input, $"@echo off\r\nsort \"{file}\"\r\n");
        }

        await WorkflowTests.OnDispatcherAsync(async () =>
        {
            var discard = true;
            var window = new MainWindow([], null, MultiBackendTests.Worker(), () => discard);
            try
            {
                window.Show();
                await WaitAsync(() => window.SetupStatusText.Text.StartsWith("Ready.", StringComparison.Ordinal));
                await window.SelectPathsAsync([input]);
                if (kind == WorkloadKind.WindowsApplication)
                {
                    window.ArgumentsBox.Text = file;
                }

                var policy = PolicySettings.Defaults(false);
                policy.Values["captureEnabled"] = "true";
                window.ApplyPolicy(policy);
                window.RunButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.IsFalse(window.ReviewFileReadButton.IsEnabled);
                await WaitAsync(() => window.BackButton.IsEnabled);
                var snapshot = window.ReportEnvironmentBox.Text;
                var rows = window.IsolationGrid.Items.Cast<IsolationEvent>().ToArray();
                var denied = rows.SingleOrDefault(row => row.NativeDenial is { Access: "read" } native && native.Resource.Equals(file, StringComparison.OrdinalIgnoreCase));
                Assert.IsFalse(window.OutputBox.Text.Contains("retry-marker", StringComparison.Ordinal));
                window.IsolationGrid.SelectedItem = denied;
                Assert.AreEqual(denied is not null, window.ReviewFileReadButton.IsEnabled);
                Assert.IsTrue(window.ChooseReadOnlyFileButton.IsEnabled);
                Assert.AreEqual(Visibility.Visible, window.ViewBlockedAccessButton.Visibility);
                window.ViewBlockedAccessButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreSame(window.IsolationTab, window.ResultsTabs.SelectedItem);
                window.UpdateLayout();
                Assert.IsTrue(window.IsolationGrid.ActualHeight >= 80, $"The access list must remain visible: {window.IsolationGrid.ActualHeight}.");
                await window.ReviewSelectedFileAsync(file, (grant, request) =>
                {
                    var dialog = new FileAccessGrantWindow(grant, request);
                    try
                    {
                        StringAssert.Contains(dialog.Preview.Text, file);
                        Assert.IsFalse(grant.FromNativeDenial);
                        StringAssert.Contains(dialog.EvidenceText.Text, "has not confirmed");
                        Assert.IsFalse(dialog.ApplyButton.IsDefault, "Enter must not implicitly grant access.");
                        Assert.AreEqual(kind, request.Kind);
                    }
                    finally
                    {
                        dialog.Close();
                    }

                    return false;
                });
                Assert.AreEqual(snapshot, window.ReportEnvironmentBox.Text, "Cancel must preserve the report and policy.");
                Assert.IsFalse(window.CurrentPolicy().Lines("readonlyPaths").Contains(file));
                if (kind == WorkloadKind.WindowsPowerShell)
                {
                    discard = false;
                    await window.ReviewSelectedFileAsync(file, (_, _) => true);
                    Assert.AreEqual(snapshot, window.ReportEnvironmentBox.Text, "Cancelling result discard must not apply the grant or rerun.");
                    Assert.IsTrue(window.ChooseReadOnlyFileButton.IsEnabled);
                    discard = true;
                }

                // Edit a different task, then return to the old report. Retry must
                // use the old command, inputs and permissions, not these edits.
                window.BackButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.ManualModeBox.IsChecked = true;
                window.ClearWorkloadButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.ProfileBox.SelectedItem = window.ProfileBox.Items.Cast<ExecutionProfile>().Single(profile => profile.Kind == WorkloadKind.WindowsBatch);
                window.ScriptBox.Text = "echo WRONG_TASK";
                window.ArgumentsBox.Text = string.Empty;
                var differentPolicy = policy.Clone();
                differentPolicy.Values["allowOutbound"] = "true";
                window.ApplyPolicy(differentPolicy);
                window.ViewResultsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.IsolationGrid.SelectedItem = denied;
                await window.ReviewSelectedFileAsync(file, (_, _) => true);
                Assert.AreEqual("Completed", window.RunPhaseText.Text, window.RunStatusText.Text + window.OutputBox.Text);
                StringAssert.Contains(window.OutputBox.Text, "retry-marker");
                Assert.IsFalse(window.OutputBox.Text.Contains("WRONG_TASK", StringComparison.Ordinal));
                Assert.IsTrue(window.CurrentPolicy().Enabled("allowOutbound"), "The separate configuration draft must remain intact.");
                StringAssert.Contains(window.ReportEnvironmentBox.Text, JsonSerializer.Serialize(file)[1..^1]);
                StringAssert.Contains(window.ReportEnvironmentBox.Text, "\"allowOutbound\": false");
                Assert.AreEqual("retry-marker\n", File.ReadAllText(file));
            }
            finally
            {
                discard = true;
                window.Close();
                await WaitAsync(() => !window.IsVisible);
            }
        });
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.Elapsed < TimeSpan.FromSeconds(60))
        {
            await Task.Delay(100);
        }

        Assert.IsTrue(condition(), "Timed out waiting for the Windows file-access workflow.");
    }
}
