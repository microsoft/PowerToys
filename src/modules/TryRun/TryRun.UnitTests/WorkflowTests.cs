// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class WorkflowTests
{
    [TestMethod]
    public async Task OpensOnConfigurationWithAnAccessibleExeChooser()
    {
        await OnDispatcherAsync(() =>
        {
            var window = new MainWindow([]);
            try
            {
                Assert.AreEqual(Visibility.Visible, window.SetupPage.Visibility);
                Assert.AreEqual(Visibility.Collapsed, window.RunPage.Visibility);
                Assert.IsTrue(window.ManualModeBox.IsChecked == true);
                Assert.AreEqual(WorkloadKind.WindowsApplication, ((ExecutionProfile)window.ProfileBox.SelectedItem).Kind);
                Assert.AreEqual(Visibility.Visible, window.ManualOptions.Visibility);
                Assert.AreEqual(Visibility.Collapsed, window.ScriptEditorPanel.Visibility);
                Assert.AreEqual(Visibility.Collapsed, window.ViewResultsButton.Visibility);
                Assert.AreEqual(Visibility.Collapsed, window.PreparationPanel.Visibility);
                for (DependencyObject? parent = window.BrowseWorkloadButton; parent is not null; parent = LogicalTreeHelper.GetParent(parent))
                {
                    Assert.IsFalse(parent is Expander, "Selecting an EXE must not require Advanced options.");
                }
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task RunsThenPreservesResultsAcrossConfigurationAndImagePreparation()
    {
        var worker = MultiBackendTests.Worker();
        await OnDispatcherAsync(async () =>
        {
            var confirmations = 0;
            var window = new MainWindow(
                [],
                null,
                worker,
                () =>
                {
                    confirmations++;
                    return true;
                });
            try
            {
                window.Show();
                await UntilAsync(() => window.SetupStatusText.Text.StartsWith("Ready.", StringComparison.Ordinal), () => window.SetupStatusText.Text);
                SelectProfile(window, WorkloadKind.WindowsPowerShell);
                window.ScriptBox.Text = "Write-Output 'workflow output'; Set-Content result.txt 'keep this result' -NoNewline";
                window.TimeoutBox.Text = "0";
                Click(window.RunButton);
                Assert.AreEqual(Visibility.Visible, window.SetupPage.Visibility);
                Assert.AreEqual(Visibility.Collapsed, window.RunPage.Visibility);
                StringAssert.Contains(window.SetupStatusText.Text, "between 1 and 300");
                window.TimeoutBox.Text = "30";
                Click(window.RunButton);
                Assert.AreEqual(Visibility.Collapsed, window.SetupPage.Visibility);
                Assert.AreEqual(Visibility.Visible, window.RunPage.Visibility);
                Assert.IsFalse(window.BackButton.IsEnabled);
                Click(window.BackButton);
                Assert.AreEqual(Visibility.Visible, window.RunPage.Visibility, "An active run cannot navigate away.");
                await UntilAsync(() => window.BackButton.IsEnabled, () => window.RunStatusText.Text);
                Assert.AreEqual("Completed", window.RunPhaseText.Text, window.RunStatusText.Text);
                StringAssert.Contains(window.OutputBox.Text, "workflow output");
                Assert.IsTrue(window.ChangesGrid.Items.Count > 0);
                Assert.IsTrue(window.ExportButton.IsEnabled);
                Assert.AreEqual(0, window.ResultsTabs.SelectedIndex, "Completion must not unexpectedly change the user's results tab.");
                var output = window.OutputBox.Text;
                var report = window.ReportEnvironmentBox.Text;
                var detail = window.RunStatusText.Text;
                var activeRun = window.ActiveRunText.Text;
                var changes = window.ChangesGrid.ItemsSource;
                Click(window.BackButton);
                Assert.AreEqual(0, confirmations, "Navigating back must not discard unexported files.");
                Assert.AreEqual(Visibility.Visible, window.SetupPage.Visibility);
                Assert.AreEqual(Visibility.Visible, window.ViewResultsButton.Visibility);
                Assert.AreEqual(Visibility.Collapsed, window.RunPage.Visibility);

                SelectProfile(window, WorkloadKind.LinuxPython);
                window.ImageBox.Text = "invalid image";
                Click(window.PrepareImageButton);
                await UntilAsync(() => window.ViewResultsButton.IsEnabled, () => window.SetupStatusText.Text);
                Assert.AreEqual(Visibility.Visible, window.SetupPage.Visibility);
                Assert.AreEqual(Visibility.Visible, window.PreparationPanel.Visibility);
                StringAssert.Contains(window.SetupStatusText.Text, "valid cached Linux image");
                Assert.AreEqual(output, window.OutputBox.Text, "Preparing a new environment must not clear the previous output.");
                Assert.AreSame(changes, window.ChangesGrid.ItemsSource);
                Click(window.ViewResultsButton);
                Assert.AreEqual(Visibility.Visible, window.RunPage.Visibility);
                Assert.AreEqual(report, window.ReportEnvironmentBox.Text);
                Assert.AreEqual(detail, window.RunStatusText.Text);
                Assert.AreEqual(activeRun, window.ActiveRunText.Text);
                Assert.IsTrue(window.ExportButton.IsEnabled);
                Assert.AreEqual(0, confirmations);

                Click(window.BackButton);
                SelectProfile(window, WorkloadKind.WindowsPowerShell);
                window.ScriptBox.Text = "Write-Output 'second run'";
                Click(window.RunButton);
                Assert.AreEqual(1, confirmations, "Replacing unexported results still needs the existing discard confirmation.");
                await UntilAsync(() => window.BackButton.IsEnabled, () => window.RunStatusText.Text);
                StringAssert.Contains(window.OutputBox.Text, "second run");
                Assert.IsFalse(window.OutputBox.Text.Contains("workflow output", StringComparison.Ordinal));
                Assert.IsFalse(window.OpenExportButton.IsEnabled);
            }
            finally
            {
                await CloseAsync(window);
            }
        });
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task ExplorerSelectionIsConfiguredBeforeItRuns()
    {
        var worker = MultiBackendTests.Worker();
        using var source = new RunSession();
        var script = Path.Combine(source.WorkingDirectory, "selected.ps1");
        File.WriteAllText(script, "Write-Output 'selected entry'");
        await OnDispatcherAsync(async () =>
        {
            var window = new MainWindow([script], null, worker, () => true);
            try
            {
                window.Show();
                await UntilAsync(() => window.RunButton.IsEnabled, () => window.SetupStatusText.Text);
                Assert.IsTrue(window.CopiedModeBox.IsChecked == true);
                Assert.AreEqual(Visibility.Visible, window.SetupPage.Visibility);
                Assert.AreEqual(Visibility.Collapsed, window.RunPage.Visibility);
                Assert.AreEqual(1, window.EntryPointBox.Items.Count);
                Assert.AreEqual(WorkloadKind.WindowsPowerShell, ((ExecutionProfile)window.ProfileBox.SelectedItem).Kind);
                Assert.IsFalse(window.ProfileBox.IsEnabled);
                Assert.AreEqual(string.Empty, window.OutputBox.Text);
                Click(window.RunButton);
                await UntilAsync(() => window.BackButton.IsEnabled, () => window.RunStatusText.Text);
                Assert.AreEqual("Completed", window.RunPhaseText.Text, window.RunStatusText.Text);
                StringAssert.Contains(window.OutputBox.Text, "selected entry");
                Assert.AreEqual("Write-Output 'selected entry'", File.ReadAllText(script));
            }
            finally
            {
                await CloseAsync(window);
            }
        });
    }

    [TestMethod]
    [DataRow("failure", "Failed")]
    [DataRow("timeout", "Time limit reached")]
    [DataRow("stop", "Stopped")]
    [TestCategory("MXCIntegration")]
    public async Task TerminalOutcomesRemainOnTheResultsPage(string operation, string expected)
    {
        var worker = MultiBackendTests.Worker();
        await OnDispatcherAsync(async () =>
        {
            var window = new MainWindow([], null, worker, () => true);
            try
            {
                window.Show();
                await UntilAsync(() => window.SetupStatusText.Text.StartsWith("Ready.", StringComparison.Ordinal), () => window.SetupStatusText.Text);
                SelectProfile(window, WorkloadKind.WindowsPowerShell);
                window.ScriptBox.Text = operation == "failure" ? "throw 'workflow failure'" : "Write-Output 'started'; Start-Sleep -Seconds 20";
                window.TimeoutBox.Text = operation == "timeout" ? "1" : "30";
                Click(window.RunButton);
                if (operation == "stop")
                {
                    await UntilAsync(() => window.OutputBox.Text.Contains("started", StringComparison.Ordinal), () => window.RunStatusText.Text);
                    Click(window.StopButton);
                }

                await UntilAsync(() => window.BackButton.IsEnabled, () => window.RunStatusText.Text);
                Assert.AreEqual(expected, window.RunPhaseText.Text, window.RunStatusText.Text);
                Assert.AreEqual(Visibility.Visible, window.RunPage.Visibility);
                Assert.AreEqual(Visibility.Collapsed, window.SetupPage.Visibility);
                Assert.AreEqual(Visibility.Collapsed, window.StopButton.Visibility);
                Assert.AreEqual(Visibility.Collapsed, window.RunProgress.Visibility);
                if (operation == "failure")
                {
                    StringAssert.Contains(window.OutputBox.Text, "workflow failure");
                }
            }
            finally
            {
                await CloseAsync(window);
            }
        });
    }

    private static void SelectProfile(MainWindow window, WorkloadKind kind) => window.ProfileBox.SelectedItem = window.ProfileBox.Items.Cast<ExecutionProfile>().Single(profile => profile.Kind == kind);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static async Task CloseAsync(MainWindow window)
    {
        window.Close();
        await UntilAsync(() => !window.IsVisible, () => window.RunStatusText.Text);
    }

    private static async Task UntilAsync(Func<bool> predicate, Func<string> detail)
    {
        var timeout = Stopwatch.StartNew();
        while (!predicate() && timeout.Elapsed < TimeSpan.FromSeconds(40))
        {
            await Task.Delay(100);
        }

        Assert.IsTrue(predicate(), "Timed out waiting for the workflow. " + detail());
    }

    private static async Task OnDispatcherAsync(Func<Task> action)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    await action();
                    completed.TrySetResult();
                }
                catch (Exception exception)
                {
                    completed.TrySetException(exception);
                }
                finally
                {
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(120));
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)));
    }
}
