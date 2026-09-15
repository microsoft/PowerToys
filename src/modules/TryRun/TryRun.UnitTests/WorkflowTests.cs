// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
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
    public async Task SingleExeSelectionAllowsHardLinksAndPreservesSupportingInputs()
    {
        using var source = new RunSession();
        var application = Path.Combine(source.WorkingDirectory, "application.EXE");
        var alias = Path.Combine(source.WorkingDirectory, "alias.exe");
        var data = Path.Combine(source.WorkingDirectory, "data.txt");
        File.Copy(Path.Combine(Environment.SystemDirectory, "findstr.exe"), application);
        File.WriteAllText(data, "supporting input");
        Assert.IsTrue(CreateHardLink(alias, application, IntPtr.Zero), $"Hard link setup failed: {Marshal.GetLastWin32Error()}");
        Assert.ThrowsException<IOException>(() => TaskBundle.Inspect([application], CancellationToken.None));
        await OnDispatcherAsync(async () =>
        {
            var window = new MainWindow([]);
            try
            {
                await window.SelectPathsAsync([data]);
                Assert.IsTrue(window.CopiedModeBox.IsChecked == true);
                await window.SelectPathsAsync([application]);
                Assert.IsTrue(window.ManualModeBox.IsChecked == true);
                Assert.AreEqual(WorkloadKind.WindowsApplication, ((ExecutionProfile)window.ProfileBox.SelectedItem).Kind);
                Assert.AreEqual(RuntimeFile.Resolve(application), window.WorkloadFileBox.Text);
                window.ArgumentsBox.Text = "arguments for the previous application";
                var systemApplication = Path.Combine(Environment.SystemDirectory, "winver.exe");
                await window.SelectPathsAsync([systemApplication]);
                Assert.AreEqual(RuntimeFile.Resolve(systemApplication), window.WorkloadFileBox.Text);
                Assert.AreEqual(string.Empty, window.ArgumentsBox.Text);
                CollectionAssert.AreEqual(new[] { data }, window.InputList.Items.Cast<string>().ToArray());
                Assert.AreEqual(Visibility.Visible, window.SetupPage.Visibility);
                Assert.AreEqual(Visibility.Collapsed, window.RunPage.Visibility);
                Assert.AreEqual(Visibility.Collapsed, window.ViewResultsButton.Visibility);
                Assert.AreEqual(string.Empty, window.OutputBox.Text);
                StringAssert.Contains(window.SetupStatusText.Text, "folder is read-only");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task FailedImportsPreserveConfigurationAndKeepCopiedInputLinkChecks()
    {
        using var source = new RunSession();
        var data = Path.Combine(source.WorkingDirectory, "data.txt");
        File.WriteAllText(data, "previous selection");
        var folder = Directory.CreateDirectory(Path.Combine(source.WorkingDirectory, "linked bundle")).FullName;
        var application = Path.Combine(folder, "application.exe");
        var alias = Path.Combine(folder, "alias.exe");
        File.Copy(Path.Combine(Environment.SystemDirectory, "findstr.exe"), application);
        Assert.IsTrue(CreateHardLink(alias, application, IntPtr.Zero), $"Hard link setup failed: {Marshal.GetLastWin32Error()}");
        await OnDispatcherAsync(async () =>
        {
            var window = new MainWindow([]);
            try
            {
                await window.SelectPathsAsync([data]);
                window.ManualModeBox.IsChecked = true;
                var entries = window.EntryPointBox.ItemsSource;
                foreach (var invalid in new[] { new[] { folder }, new[] { application, data }, new[] { Path.Combine(folder, "missing.exe") } })
                {
                    await window.SelectPathsAsync(invalid);
                    StringAssert.StartsWith(window.SetupStatusText.Text, "Could not import selection:");
                    Assert.IsTrue(window.ManualModeBox.IsChecked == true, "A rejected import must not switch to copied mode.");
                    Assert.AreEqual(string.Empty, window.WorkloadFileBox.Text);
                    CollectionAssert.AreEqual(new[] { data }, window.InputList.Items.Cast<string>().ToArray());
                    Assert.AreSame(entries, window.EntryPointBox.ItemsSource);
                }

                await window.SelectPathsAsync([application]);
                var previousApplication = window.WorkloadFileBox.Text;
                window.ArgumentsBox.Text = "keep arguments";
                await window.SelectPathsAsync([Path.Combine(folder, "missing.exe")]);
                Assert.AreEqual(previousApplication, window.WorkloadFileBox.Text);
                Assert.AreEqual("keep arguments", window.ArgumentsBox.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task FolderAndMultipleFileSelectionsKeepTheCopiedWorkflow(bool selectFolder)
    {
        using var source = new RunSession();
        var folder = Directory.CreateDirectory(Path.Combine(source.WorkingDirectory, "bundle.exe")).FullName;
        var application = Path.Combine(folder, "findstr.exe");
        var data = Path.Combine(folder, "data.txt");
        File.Copy(Path.Combine(Environment.SystemDirectory, "findstr.exe"), application);
        File.WriteAllText(data, "copied input");
        await OnDispatcherAsync(async () =>
        {
            var window = new MainWindow([]);
            try
            {
                await window.SelectPathsAsync(selectFolder ? [folder] : [application, data]);
                Assert.IsTrue(window.CopiedModeBox.IsChecked == true);
                Assert.AreEqual(WorkloadKind.WindowsApplication, ((ExecutionProfile)window.ProfileBox.SelectedItem).Kind);
                Assert.AreEqual(string.Empty, window.WorkloadFileBox.Text);
                Assert.AreEqual(1, window.EntryPointBox.Items.Count);
                Assert.IsNotNull(window.EntryPointBox.SelectedItem);
                Assert.AreEqual(selectFolder ? 1 : 2, window.InputList.Items.Count);
                StringAssert.StartsWith(window.SetupStatusText.Text, "Entry point selected.");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task ExplorerExeSelectionAndDroppedApplicationUseTheInstalledApplicationWorkflow()
    {
        var worker = MultiBackendTests.Worker();
        using var source = new RunSession();
        var data = Path.Combine(source.WorkingDirectory, "data.txt");
        File.WriteAllText(data, "dropped application input\n");
        await OnDispatcherAsync(async () =>
        {
            var application = Path.Combine(Environment.SystemDirectory, "winver.exe");
            var window = new MainWindow([application], null, worker, () => true);
            try
            {
                window.Show();
                await UntilAsync(() => window.RunButton.IsEnabled, () => window.SetupStatusText.Text);
                Assert.IsTrue(window.ManualModeBox.IsChecked == true);
                Assert.AreEqual(RuntimeFile.Resolve(application), window.WorkloadFileBox.Text);
                Assert.AreEqual(0, window.InputList.Items.Count);
                Assert.AreEqual(Visibility.Collapsed, window.RunPage.Visibility);
                Assert.AreEqual(string.Empty, window.OutputBox.Text);
                await window.SelectPathsAsync([data]);
                await window.SelectPathsAsync([Path.Combine(Environment.SystemDirectory, "findstr.exe")]);
                window.ArgumentsBox.Text = "/c:input\ndata.txt";
                Click(window.RunButton);
                var selectedApplication = window.WorkloadFileBox.Text;
                await window.SelectPathsAsync([application]);
                Assert.AreEqual(selectedApplication, window.WorkloadFileBox.Text, "Drops must be ignored during an active run.");
                await UntilAsync(() => window.BackButton.IsEnabled, () => window.RunStatusText.Text);
                Assert.AreEqual("Completed", window.RunPhaseText.Text, window.RunStatusText.Text + "\n" + window.OutputBox.Text);
                StringAssert.Contains(window.OutputBox.Text, "dropped application input");
                StringAssert.Contains(window.ReportEnvironmentBox.Text, "ProcessContainer");
                Assert.AreEqual("dropped application input\n", File.ReadAllText(data));
                var output = window.OutputBox.Text;
                await window.SelectPathsAsync([application]);
                Assert.AreEqual(selectedApplication, window.WorkloadFileBox.Text, "The results page must ignore drops even after completion.");
                Click(window.BackButton);
                await window.SelectPathsAsync([application]);
                Assert.AreEqual(RuntimeFile.Resolve(application), window.WorkloadFileBox.Text);
                Assert.AreEqual(output, window.OutputBox.Text, "Configuring an application must preserve previous results.");
            }
            finally
            {
                await CloseAsync(window);
            }
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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);

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
