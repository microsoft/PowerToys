// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF owns the window lifetime. Closing cancels active work and disposes the session; the run's finally block disposes its cancellation source.")]
public partial class MainWindow : Window
{
    private readonly string workerPath;
    private readonly Func<bool>? confirmDiscard;
    private readonly ObservableCollection<string> inputs = [];
    private readonly HashSet<string> unexported = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<WorkloadKind, (string Script, string File, string Arguments, string Interpreter)> drafts = [];
    private readonly string[] startupPaths;
    private readonly string? startupError;
    private CancellationTokenSource? cancellation;
    private RunSession? session;
    private bool closeWhenStopped;
    private bool canProbe;
    private bool available;
    private bool canReview;
    private bool hasUnreviewedResults;
    private FileWorkspace? workspace;
    private List<FileChangeRow> changes = [];
    private string? exportDirectory;
    private BackendAvailability? backends;
    private bool windowsReady;
    private string? windowsFailure;
    private WorkloadKind kind;
    private bool updatingSelection;
    private bool isolationDemo;
    private IsolationReport? isolationReport;
    private RunWindowBorders? windowBorders;
    private bool showingRun;
    private bool hasRunResult;

    public MainWindow(string[] startupPaths, string? startupError = null)
        : this(startupPaths, startupError, null, null)
    {
    }

    internal MainWindow(string[] startupPaths, string? startupError, string? workerExecutable, Func<bool>? confirmDiscard)
    {
        workerPath = workerExecutable ?? Path.Combine(AppContext.BaseDirectory, "Worker", "PowerToys.TryRun.Worker.exe");
        this.confirmDiscard = confirmDiscard;
        this.startupPaths = startupPaths;
        this.startupError = startupError;
        InitializeComponent();
        InputList.ItemsSource = inputs;
        ProfileBox.ItemsSource = new[]
        {
            new ExecutionProfile(WorkloadKind.WindowsPowerShell, "Windows · PowerShell"),
            new ExecutionProfile(WorkloadKind.WindowsApplication, "Windows · Application (.exe)"),
            new ExecutionProfile(WorkloadKind.WindowsBatch, "Windows · Batch (.cmd / .bat)"),
            new ExecutionProfile(WorkloadKind.LinuxShell, "Linux · Shell"),
            new ExecutionProfile(WorkloadKind.LinuxPython, "Linux · Python / other runtime"),
            new ExecutionProfile(WorkloadKind.LinuxApplication, "Linux · Executable"),
        };
        ProfileBox.SelectedIndex = startupPaths.Length == 0 ? 1 : 0;
        if (startupPaths.Length == 0)
        {
            ManualModeBox.IsChecked = true;
        }
        else
        {
            CopiedModeBox.IsChecked = true;
        }

        ShowPage(false);
        var reason = RuntimeRequirements.GetUnavailableReason();
        if (reason is null && !File.Exists(workerPath))
        {
            reason = "Build the MXC execution worker before running scripts. See src/modules/TryRun/README.md.";
        }

        if (reason is not null)
        {
            Status = reason;
            RunButton.IsEnabled = false;
        }
        else
        {
            canProbe = true;
        }
    }

    private string Status
    {
        get => showingRun ? RunStatusText.Text : SetupStatusText.Text;
        set
        {
            if (showingRun)
            {
                RunStatusText.Text = value;
            }
            else
            {
                SetupStatusText.Text = value;
            }
        }
    }

    private void ShowPage(bool run)
    {
        showingRun = run;
        SetupPage.Visibility = run ? Visibility.Collapsed : Visibility.Visible;
        RunPage.Visibility = run ? Visibility.Visible : Visibility.Collapsed;
        ConfigureStepText.FontWeight = run ? FontWeights.Normal : FontWeights.SemiBold;
        ResultsStepText.FontWeight = run ? FontWeights.SemiBold : FontWeights.Normal;
        ConfigureStepText.Opacity = run ? 0.5 : 1;
        ResultsStepText.Opacity = run ? 1 : 0.5;
        ViewResultsButton.Visibility = hasRunResult ? Visibility.Visible : Visibility.Collapsed;
        SetRunning(cancellation is not null);
    }

    private void OnBackToSetup(object sender, RoutedEventArgs e)
    {
        if (cancellation is null)
        {
            ShowPage(false);
            Status = "Adjust the configuration for your next run. The previous run's results are still available.";
        }
    }

    private void OnViewResults(object sender, RoutedEventArgs e)
    {
        if (cancellation is null && hasRunResult)
        {
            ShowPage(true);
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (!canProbe)
        {
            if (startupError is not null)
            {
                Status = startupError;
            }

            if (startupPaths.Length > 0)
            {
                await ImportSelectionAsync(startupPaths);
            }

            return;
        }

        canProbe = false;
        cancellation = new CancellationTokenSource();
        SetRunning(true);
        Status = "Checking restricted workspace access…";
        try
        {
            var client = new WorkerClient(workerPath);
            backends = await client.GetBackendsAsync(cancellation.Token);
            if (backends.WindowsAvailable)
            {
                windowsFailure = await client.GetAvailabilityFailureAsync(cancellation.Token);
                windowsReady = windowsFailure is null;
            }

            UpdateProfileState();
            Status = available ? "Ready. Choose a program or add files to begin." : BackendText.Text;
        }
        catch (OperationCanceledException)
        {
            Status = "Availability check stopped. Reopen Try Run to check again.";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
        finally
        {
            FinishOperation();
        }

        if (startupPaths.Length > 0 && !closeWhenStopped && IsVisible)
        {
            await ImportSelectionAsync(startupPaths);
        }

        if (startupError is not null && IsVisible)
        {
            Status = startupError;
        }
    }

    private void OnProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || ProfileBox.SelectedItem is not ExecutionProfile profile)
        {
            return;
        }

        drafts[kind] = (ScriptBox.Text, WorkloadFileBox.Text, ArgumentsBox.Text, InterpreterBox.Text);
        kind = profile.Kind;
        var initial = kind switch
        {
            WorkloadKind.WindowsPowerShell => "Get-ChildItem -Recurse",
            WorkloadKind.WindowsBatch => "@echo off\r\necho Hello from Windows\r\nver",
            WorkloadKind.LinuxShell => "uname -s\nprintf 'Hello from Linux\\n'\nls -la",
            WorkloadKind.LinuxPython => "import platform\nprint('Hello from', platform.system())",
            _ => string.Empty,
        };
        var draft = drafts.GetValueOrDefault(kind, (initial, string.Empty, string.Empty, kind == WorkloadKind.LinuxPython ? "python3" : "/bin/sh"));
        ScriptBox.Text = draft.Item1;
        WorkloadFileBox.Text = draft.Item2;
        ArgumentsBox.Text = draft.Item3;
        InterpreterBox.Text = draft.Item4;
        if (kind == WorkloadKind.LinuxPython && ImageBox.Text == "alpine:3.22")
        {
            ImageBox.Text = "python:3.12-alpine";
        }

        UpdateProfileState();
        UpdateRunSummary();
        SetRunning(cancellation is not null);
    }

    private void UpdateProfileState()
    {
        var linux = kind is WorkloadKind.LinuxShell or WorkloadKind.LinuxPython or WorkloadKind.LinuxApplication;
        available = linux ? backends?.LinuxAvailable == true : windowsReady;
        LinuxOptions.Visibility = linux ? Visibility.Visible : Visibility.Collapsed;
        LinuxImageHeader.Visibility = LinuxOptions.Visibility;
        BackendText.Text = linux ? backends?.LinuxDetail ?? "Checking Linux backend…" : windowsFailure ?? backends?.WindowsDetail ?? "Checking Windows backend…";
        PermissionText.Text = linux ? "Linux: isolated WSLC container, 2 CPUs / 2 GiB. Only this run's data folders are mounted. No existing WSL distribution is used." : "Windows: MXC ProcessContainer. Copied applications use the run workspace; installed application directories are read-only. Clipboard and input injection: off. Windows are allowed.";
        CaptureOption.Visibility = linux ? Visibility.Collapsed : Visibility.Visible;
        CaptureHint.Text = backends?.NativeDenialCaptureAvailable == true ? "Native MXC capture is available. Blocked access stays blocked." : "Native capture unavailable on this host. Runs keep their restrictions; no elevated capture fallback is used.";
        if (backends is not null && cancellation is null)
        {
            SetupStatusText.Text = available ? "Ready. Choose a program or add files to begin." : BackendText.Text;
        }

        UpdateEnvironmentSummary();
    }

    private void OnBrowseWorkload(object sender, RoutedEventArgs e)
    {
        var filter = kind switch
        {
            WorkloadKind.WindowsApplication => "Windows applications|*.exe",
            WorkloadKind.WindowsPowerShell => "PowerShell scripts|*.ps1|All files|*.*",
            WorkloadKind.WindowsBatch => "Batch scripts|*.cmd;*.bat|All files|*.*",
            WorkloadKind.LinuxPython => "Python scripts|*.py|All files|*.*",
            _ => "All files|*.*",
        };
        var dialog = new OpenFileDialog { Title = "Choose a program or script file", Filter = filter, CheckFileExists = true };
        if (dialog.ShowDialog(this) == true)
        {
            WorkloadFileBox.Text = dialog.FileName;
            SetRunning(false);
        }
    }

    private void OnClearWorkload(object sender, RoutedEventArgs e)
    {
        WorkloadFileBox.Clear();
        SetRunning(false);
    }

    private void OnWorkloadChanged(object sender, TextChangedEventArgs e)
    {
        if (IsInitialized)
        {
            SetRunning(cancellation is not null);
        }
    }

    private void OnBrowseImage(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Choose a local container image archive", Filter = "Image archives|*.tar;*.tar.gz;*.tgz|All files|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true)
        {
            ImageTarBox.Text = dialog.FileName;
        }
    }

    private async void OnPrepareImage(object sender, RoutedEventArgs e)
    {
        if (showingRun || cancellation is not null)
        {
            return;
        }

        cancellation = new CancellationTokenSource();
        SetRunning(true);
        PreparationPanel.Visibility = Visibility.Visible;
        PreparationOutputBox.Clear();
        Status = "Preparing image using MXC. This step downloads into the dedicated image cache…";
        var buffer = new OutputBuffer();
        try
        {
            using var preparation = new RunSession();
            var request = new ExecutionRequest(":", preparation.WorkingDirectory, preparation.TemporaryDirectory, 300)
            {
                Kind = WorkloadKind.LinuxShell,
                Image = ImageBox.Text.Trim(),
                PrepareImage = true,
            };
            var progress = new Progress<WorkerMessage>(message =>
            {
                if (message.Kind is WorkerMessage.Output or WorkerMessage.Error)
                {
                    buffer.Append(message.Text);
                    PreparationOutputBox.Text = buffer.ToString();
                    PreparationOutputBox.ScrollToEnd();
                }
            });
            var result = await new WorkerClient(workerPath).RunAsync(request, progress, cancellation.Token);
            Status = result.ExitCode == 0 ? "Image ready. Runs use the cached image with networking off." : "Image preparation failed. See the preparation log for details.";
        }
        catch (OperationCanceledException)
        {
            Status = "Image preparation stopped.";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
        finally
        {
            FinishOperation();
        }
    }

    private async void OnAddFiles(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Choose files to copy into this run", Multiselect = true, CheckFileExists = true };
        if (dialog.ShowDialog(this) == true)
        {
            SelectCopiedModeForEmptyApplication();
            await ImportSelectionAsync(inputs.Concat(dialog.FileNames));
        }
    }

    private async void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose a folder to copy into this run" };
        if (dialog.ShowDialog(this) == true)
        {
            SelectCopiedModeForEmptyApplication();
            await ImportSelectionAsync(inputs.Append(dialog.FolderName));
        }
    }

    private void SelectCopiedModeForEmptyApplication()
    {
        if (kind == WorkloadKind.WindowsApplication && string.IsNullOrWhiteSpace(WorkloadFileBox.Text))
        {
            CopiedModeBox.IsChecked = true;
        }
    }

    private async Task ImportSelectionAsync(IEnumerable<string> paths)
    {
        isolationDemo = false;
        var selection = paths.ToArray();
        var previous = (EntryPointBox.SelectedItem as TaskEntryPoint)?.RelativePath;
        cancellation = new CancellationTokenSource();
        SetRunning(true);
        Status = "Reading selection and identifying entry points…";
        try
        {
            var inspected = await Task.Run(() => TaskBundle.Inspect(selection, cancellation.Token));
            inputs.Clear();
            foreach (var path in inspected.Inputs)
            {
                inputs.Add(path);
            }

            updatingSelection = true;
            EntryPointBox.ItemsSource = inspected.EntryPoints;
            EntryPointBox.SelectedItem = inspected.EntryPoints.FirstOrDefault(entry => entry.RelativePath == previous) ?? (inspected.EntryPoints.Count == 1 ? inspected.EntryPoints[0] : null);
            updatingSelection = false;
            ApplyEntryPoint();
            SelectionText.Text = $"{inspected.EntryCount} files and folders · {inspected.Bytes / 1048576.0:F1} MiB. Directory structure is preserved.";
            Status = inspected.EntryPoints.Count switch
            {
                0 => "No supported entry point found. Add a program/script, or select Installed app / inline script.",
                1 => "Entry point selected. Review the run summary, then select Run.",
                _ => "Choose one entry point. The other selected files accompany it as data or dependencies.",
            };
        }
        catch (OperationCanceledException)
        {
            Status = "Selection inspection stopped. The previous selection is unchanged.";
        }
        catch (Exception exception)
        {
            Status = "Could not import selection: " + exception.Message;
        }
        finally
        {
            updatingSelection = false;
            FinishOperation();
        }
    }

    private async void OnRemoveInput(object sender, RoutedEventArgs e)
    {
        var removed = InputList.SelectedItems.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        await ImportSelectionAsync(inputs.Where(path => !removed.Contains(path)));
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = !showingRun && cancellation is null && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!showingRun && cancellation is null && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            SelectCopiedModeForEmptyApplication();
            await ImportSelectionAsync(inputs.Concat(paths));
        }
    }

    private void OnEntryPointChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!updatingSelection)
        {
            ApplyEntryPoint();
            SetRunning(cancellation is not null);
        }
    }

    private void ApplyEntryPoint()
    {
        if (EntryPointBox.SelectedItem is TaskEntryPoint entry && ManualModeBox.IsChecked != true)
        {
            ProfileBox.SelectedItem = ProfileBox.Items.Cast<ExecutionProfile>().First(profile => profile.Kind == entry.Kind);
            if (entry.Interpreter is not null)
            {
                InterpreterBox.Text = entry.Interpreter;
            }
        }

        UpdateRunSummary();
    }

    private void OnManualModeChanged(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
        {
            isolationDemo = false;
            ApplyEntryPoint();
            SetRunning(cancellation is not null);
        }
    }

    private void UpdateRunSummary()
    {
        RunSummary.Text = ManualModeBox.IsChecked == true
            ? kind == WorkloadKind.WindowsApplication ? "Choose an installed EXE. Its application folder is read-only; selected input data runs on copies." : "Choose a script file or write one here. Selected files are copied before running."
            : EntryPointBox.SelectedItem is TaskEntryPoint entry
                ? $"Run {entry.RelativePath}\nWorking folder: {entry.WorkingSubdirectory ?? "selection root"}. All selected files are copied. The original folders are not granted access. Changes stay in this run until you export them."
                : "Drop files or a folder, then choose an entry point. Nothing runs until you select Run.";
        UpdateEnvironmentSummary();
    }

    private void OnEnvironmentChanged(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
        {
            UpdateEnvironmentSummary();
        }
    }

    private void UpdateEnvironmentSummary()
    {
        var linux = kind is WorkloadKind.LinuxShell or WorkloadKind.LinuxPython or WorkloadKind.LinuxApplication;
        var installed = ManualModeBox.IsChecked == true && kind == WorkloadKind.WindowsApplication;
        PolicySummaryText.Text = "Network: off\n" + (installed ? "Installed application folder: read-only\nInput data: writable copies" : "Input files: writable copies") + (linux ? "\nLinux environment: 2 CPUs / 2 GiB" : "\nWindows applications: cyan window border");
        var capture = linux ? "Windows denial capture does not apply to WSLC." : CaptureBox.IsChecked != true ? "Denial capture off." : backends?.NativeDenialCaptureAvailable == true ? "Native denial capture: block and record." : "Native denial capture unavailable.";
        var runtime = linux ? $"Linux / MXC WSLC · {ImageBox.Text}\nRuntime: {(kind == WorkloadKind.LinuxApplication ? "Selected executable" : InterpreterBox.Text)} · 2 CPUs / 2 GiB" : "Windows / MXC ProcessContainer · " + (ProfileBox.SelectedItem as ExecutionProfile)?.Name;
        EnvironmentSummaryText.Text = $"{runtime}\nFiles: selected copies and temporary data are writable. {(installed ? "Selected installation folder is read-only." : "Original input folders are not granted access.")}\nNetwork: off · Time limit: {TimeoutBox.Text} seconds\n{capture}" + (isolationDemo ? "\nDemo: a harmless host-only fixture is supplied for an access check." : string.Empty);
    }

    private async void OnWindowsDemo(object sender, RoutedEventArgs e) => await LoadDemoAsync("Windows-isolation");

    private async void OnLinuxDemo(object sender, RoutedEventArgs e) => await LoadDemoAsync("Linux-isolation");

    private async Task LoadDemoAsync(string name)
    {
        CopiedModeBox.IsChecked = true;
        await ImportSelectionAsync([Path.Combine(AppContext.BaseDirectory, "Samples", name)]);
        isolationDemo = inputs.Count == 1 && Path.GetFileName(inputs[0]) == name;
        if (isolationDemo)
        {
            ArgumentsBox.Clear();
            if (name == "Linux-isolation")
            {
                ImageBox.Text = "alpine:3.22";
            }

            Status = "Demo ready. Select Run to change a copy, check access to a harmless host-only file, and review the isolation report.";
            UpdateEnvironmentSummary();
        }
    }

    private void ShowIsolationReport(IsolationReport report)
    {
        isolationReport = report;
        CaptureStatusText.Text = $"Denial capture: {report.CaptureStatus}\n{report.CaptureDetail}";
        ReportEvidenceText.Text = report.EvidenceNote;
        IsolationGrid.ItemsSource = report.Events;
        if (report.Events.Count > 0)
        {
            IsolationGrid.SelectedIndex = 0;
        }
    }

    private void OnIsolationEventSelected(object sender, SelectionChangedEventArgs e)
    {
        IsolationDetails.Text = IsolationGrid.SelectedItem is IsolationEvent observation ? $"{observation.Source}\n{observation.Resource}\n{observation.Detail}" : string.Empty;
    }

    private async void OnRun(object sender, RoutedEventArgs e)
    {
        if (cancellation is not null || showingRun)
        {
            return;
        }

        if (!int.TryParse(TimeoutBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var timeout) || timeout is < 1 or > 300)
        {
            Status = "Choose a timeout between 1 and 300 seconds.";
            return;
        }

        var entry = ManualModeBox.IsChecked == true ? null : EntryPointBox.SelectedItem as TaskEntryPoint;
        if (ManualModeBox.IsChecked != true && entry is null)
        {
            Status = "Choose an entry point first.";
            return;
        }

        if (ManualModeBox.IsChecked == true && string.IsNullOrWhiteSpace(WorkloadFileBox.Text) && (kind is WorkloadKind.WindowsApplication or WorkloadKind.LinuxApplication || string.IsNullOrWhiteSpace(ScriptBox.Text)))
        {
            Status = "Choose a program or enter a script first.";
            return;
        }

        if (!ConfirmDiscard())
        {
            return;
        }

        hasRunResult = true;
        ShowPage(true);
        RunPhaseText.Text = "Preparing files";
        ActiveRunText.Text = $"{entry?.RelativePath ?? (string.IsNullOrWhiteSpace(WorkloadFileBox.Text) ? "Inline script" : WorkloadFileBox.Text)} · {(ProfileBox.SelectedItem as ExecutionProfile)?.Name} · {timeout}s limit";
        cancellation = new CancellationTokenSource();
        SetRunning(true);
        changes = [];
        exportDirectory = null;
        unexported.Clear();
        canReview = false;
        hasUnreviewedResults = false;
        ChangesGrid.ItemsSource = changes;
        BeforePreview.Clear();
        AfterPreview.Clear();
        ChangesSummary.Text = "Copying inputs into a fresh workspace…";
        ResultsTabs.SelectedIndex = 0;
        var buffer = new OutputBuffer();
        OutputBox.Clear();
        FilesTab.Header = "_Files";
        Status = "Copying inputs…";
        ReportEnvironmentBox.Text = "Preparing this run's environment…";
        OriginalCheckText.Text = "Original file check pending.";
        IsolationDetails.Clear();
        ShowIsolationReport(new IsolationReport("Pending", "The run has not started.", []));
        var workloadFile = entry is null ? WorkloadFileBox.Text : string.Empty;
        var inputPaths = inputs.Concat(!string.IsNullOrWhiteSpace(workloadFile) && kind != WorkloadKind.WindowsApplication ? new[] { workloadFile } : []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var prepared = false;
        var finalPhase = "Failed";
        using var runBorders = new RunWindowBorders(message => WindowBorderText.Text = message);
        windowBorders = runBorders;
        WindowBorderText.Visibility = kind is WorkloadKind.LinuxShell or WorkloadKind.LinuxPython or WorkloadKind.LinuxApplication ? Visibility.Collapsed : Visibility.Visible;
        WindowBorderText.Text = "Windows in this run will have a cyan border after MXC starts the application.";
        try
        {
            session?.Dispose();
            session = new RunSession();
            workspace = new FileWorkspace(session);
            var runBundle = await Task.Run(() => TaskBundle.Inspect(inputPaths, cancellation.Token));
            if (entry is not null && !runBundle.EntryPoints.Any(candidate => candidate.RelativePath == entry.RelativePath && candidate.Kind == entry.Kind))
            {
                throw new IOException("The selected entry point changed or was removed. Select the files again.");
            }

            var relativeFile = entry?.RelativePath ?? (kind != WorkloadKind.WindowsApplication && !string.IsNullOrWhiteSpace(workloadFile) ? runBundle.GetRelativePath(workloadFile) : null);
            await Task.Run(() => workspace.Import(runBundle.Inputs, cancellation.Token));
            prepared = true;
            canReview = true;
            hasUnreviewedResults = true;
            Status = "Starting a restricted run…";
            RunPhaseText.Text = "Starting";
            var request = new ExecutionRequest(entry is null ? ScriptBox.Text : string.Empty, session.WorkingDirectory, session.TemporaryDirectory, timeout)
            {
                Kind = kind,
                ApplicationPath = entry is null && kind == WorkloadKind.WindowsApplication ? workloadFile : null,
                FileRelativePath = relativeFile,
                WorkingSubdirectory = entry?.WorkingSubdirectory,
                Arguments = ArgumentsBox.Text.Length == 0 ? [] : ArgumentsBox.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'),
                Image = ImageBox.Text.Trim(),
                ImageTarPath = kind is WorkloadKind.LinuxShell or WorkloadKind.LinuxPython or WorkloadKind.LinuxApplication && !string.IsNullOrWhiteSpace(ImageTarBox.Text) ? ImageTarBox.Text.Trim() : null,
                Interpreter = kind is WorkloadKind.LinuxShell or WorkloadKind.LinuxPython ? InterpreterBox.Text.Trim() : null,
                CaptureDenials = kind is not WorkloadKind.LinuxShell and not WorkloadKind.LinuxPython and not WorkloadKind.LinuxApplication && CaptureBox.IsChecked == true,
                IsolationDemo = isolationDemo,
            };
            var environmentReceived = false;
            void MarkRunning()
            {
                if (RunPhaseText.Text == "Starting" && cancellation?.IsCancellationRequested == false)
                {
                    RunPhaseText.Text = "Running";
                    Status = "Running. Output and errors appear below.";
                }
            }

            var progress = new Progress<WorkerMessage>(message =>
            {
                if (message.Kind == WorkerMessage.ProcessStarted && message.Process is { } process && !request.IsLinux)
                {
                    MarkRunning();
                    WindowBorderText.Text = "Cyan borders identify windows belonging to this run.";
                    runBorders.Start(process);
                }

                if (message.Kind == WorkerMessage.Completed)
                {
                    runBorders.Dispose();
                }

                if (message.Environment is { } environment)
                {
                    environmentReceived = true;
                    ReportEnvironmentBox.Text = environment.Describe();
                }

                if (message.Report is { } report)
                {
                    ShowIsolationReport(report);
                }

                if (message.Kind is WorkerMessage.Output or WorkerMessage.Error)
                {
                    if (environmentReceived)
                    {
                        MarkRunning();
                    }

                    buffer.Append(message.Text);
                    OutputBox.Text = buffer.ToString();
                    OutputBox.ScrollToEnd();
                }
            });
            var result = await new WorkerClient(workerPath).RunAsync(request, progress, cancellation.Token);
            finalPhase = result.TimedOut ? "Time limit reached" : result.ExitCode == 0 ? "Completed" : "Failed";
            Status = result.TimedOut ? "Stopped: time limit reached." : result.ExitCode == 0 ? "Finished. Exit code: 0." : $"Run failed. Exit code: {result.ExitCode}. See the output for details.";
        }
        catch (OperationCanceledException)
        {
            finalPhase = "Stopped";
            Status = "Stopped.";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
        finally
        {
            runBorders.Dispose();
            windowBorders = null;
            WindowBorderText.Visibility = Visibility.Collapsed;
            if (prepared && !closeWhenStopped)
            {
                RunPhaseText.Text = "Reviewing files";
                if (isolationReport?.CaptureStatus is "Pending" or "Collecting")
                {
                    ShowIsolationReport(new IsolationReport("Incomplete", "The worker did not finish report collection. Available output and copied files can still be reviewed.", []));
                }

                var outcome = Status;
                cancellation.Dispose();
                cancellation = new CancellationTokenSource();
                StopButton.IsEnabled = true;
                await ReviewResultsAsync(outcome);
            }

            RunPhaseText.Text = finalPhase;
            FinishOperation();
        }
    }

    private async void OnRefreshReview(object sender, RoutedEventArgs e)
    {
        cancellation = new CancellationTokenSource();
        SetRunning(true);
        try
        {
            await ReviewResultsAsync("Review refreshed.");
        }
        finally
        {
            FinishOperation();
        }
    }

    private async Task ReviewResultsAsync(string outcome)
    {
        hasUnreviewedResults = true;
        changes = [];
        ChangesGrid.ItemsSource = changes;
        BeforePreview.Clear();
        AfterPreview.Clear();
        Status = "Reviewing result files…";
        try
        {
            var originals = await Task.Run(() => workspace!.CheckOriginals(cancellation!.Token));
            OriginalCheckText.Text = originals.ToString();
            var comparison = await Task.Run(() => workspace!.Review(cancellation!.Token));
            changes = comparison.Select(change => new FileChangeRow(change)).ToList();
            ChangesGrid.ItemsSource = changes;
            unexported.Clear();
            foreach (var change in changes.Where(change => change.IsSelected))
            {
                unexported.Add(change.RelativePath);
            }

            hasUnreviewedResults = false;
            ChangesSummary.Text = string.Join(" · ", Enum.GetValues<FileChangeKind>().Select(kind => $"{changes.Count(change => change.Kind == kind)} {kind.ToString().ToLowerInvariant()}"));
            if (changes.Count > 0)
            {
                ChangesGrid.SelectedItem = changes.FirstOrDefault(change => change.Kind != FileChangeKind.Unchanged) ?? changes[0];
            }

            FilesTab.Header = $"_Files ({changes.Count(change => change.Kind != FileChangeKind.Unchanged)})";
            Status = outcome + " Review files before exporting.";
        }
        catch (Exception exception)
        {
            ChangesSummary.Text = "File review did not finish. Export is unavailable; choose Refresh review to retry.";
            Status = outcome + " " + exception.Message;
        }
    }

    private void OnChangeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ChangesGrid.SelectedItem is FileChangeRow row)
        {
            BeforePreview.Text = row.BeforePreview;
            AfterPreview.Text = row.AfterPreview;
        }
    }

    private async void OnExport(object sender, RoutedEventArgs e)
    {
        var selected = changes.Where(change => change.IsSelected && change.CanExport).Select(change => change.RelativePath).ToArray();
        if (selected.Length == 0)
        {
            Status = "Check one or more result files to export.";
            return;
        }

        var dialog = new OpenFolderDialog { Title = "Choose where to create a new results folder" };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        cancellation = new CancellationTokenSource();
        SetRunning(true);
        Status = "Verifying and exporting checked files…";
        try
        {
            exportDirectory = await Task.Run(() => workspace!.Export(dialog.FolderName, selected, cancellation.Token));
            unexported.ExceptWith(selected);
            Status = $"Exported {selected.Length} files to: {exportDirectory}";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
        finally
        {
            FinishOperation();
        }
    }

    private void OnOpenExport(object sender, RoutedEventArgs e)
    {
        if (exportDirectory is not null)
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { UseShellExecute = false };
            start.ArgumentList.Add(exportDirectory);
            Process.Start(start)?.Dispose();
        }
    }

    private void OnStop(object sender, RoutedEventArgs e)
    {
        windowBorders?.Dispose();
        Status = "Stopping…";
        if (showingRun)
        {
            RunPhaseText.Text = "Stopping";
        }

        StopButton.IsEnabled = false;
        SetupStopButton.IsEnabled = false;
        cancellation?.Cancel();
    }

    private void SetRunning(bool running)
    {
        var manual = ManualModeBox.IsChecked == true;
        ManualOptions.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
        CopiedEntryPanel.Visibility = manual ? Visibility.Collapsed : Visibility.Visible;
        ScriptEditorPanel.Visibility = manual && kind is not WorkloadKind.WindowsApplication and not WorkloadKind.LinuxApplication && string.IsNullOrWhiteSpace(WorkloadFileBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        ProfileHint.Text = manual ? "Choose the environment your program needs." : "Detected from the selected entry point.";
        CopiedModeBox.IsEnabled = !running;
        BackButton.IsEnabled = !running;
        ViewResultsButton.IsEnabled = !running;
        SetupStopButton.IsEnabled = running;
        SetupStopButton.Visibility = running && !showingRun ? Visibility.Visible : Visibility.Collapsed;
        StopButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        RunProgress.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        WindowsDemoButton.IsEnabled = !running;
        LinuxDemoButton.IsEnabled = !running;
        CaptureBox.IsEnabled = !running && backends?.NativeDenialCaptureAvailable == true;
        var hasCommand = !string.IsNullOrWhiteSpace(WorkloadFileBox.Text) || (kind is not WorkloadKind.WindowsApplication and not WorkloadKind.LinuxApplication && !string.IsNullOrWhiteSpace(ScriptBox.Text));
        RunButton.IsEnabled = available && !running && (manual ? hasCommand : EntryPointBox.SelectedItem is TaskEntryPoint);
        EntryPointBox.IsEnabled = !running && !manual;
        ManualModeBox.IsEnabled = !running;
        ManualOptions.IsEnabled = !running && manual;
        StopButton.IsEnabled = running;
        ScriptBox.IsEnabled = !running && WorkloadFileBox.Text.Length == 0 && kind is not WorkloadKind.WindowsApplication and not WorkloadKind.LinuxApplication;
        TimeoutBox.IsEnabled = !running;
        AddFilesButton.IsEnabled = !running;
        AddFolderButton.IsEnabled = !running;
        RemoveInputButton.IsEnabled = !running && inputs.Count > 0;
        InputList.IsEnabled = !running;
        ChangesGrid.IsEnabled = !running;
        ExportButton.IsEnabled = !running && changes.Any(change => change.CanExport);
        ReviewButton.IsEnabled = !running && canReview;
        OpenExportButton.IsEnabled = !running && exportDirectory is not null;
        ProfileBox.IsEnabled = !running && manual;
        BrowseWorkloadButton.IsEnabled = !running;
        ClearWorkloadButton.IsEnabled = !running && WorkloadFileBox.Text.Length > 0;
        ArgumentsBox.IsEnabled = !running;
        ImageBox.IsEnabled = !running;
        ImageTarBox.IsEnabled = !running;
        InterpreterBox.IsEnabled = !running && kind != WorkloadKind.LinuxApplication;
        BrowseImageButton.IsEnabled = !running;
        PrepareImageButton.IsEnabled = !running && backends?.LinuxAvailable == true;
    }

    private void FinishOperation()
    {
        cancellation?.Dispose();
        cancellation = null;
        SetRunning(false);
        if (closeWhenStopped)
        {
            closeWhenStopped = false;
            Close();
        }
    }

    private bool ConfirmDiscard()
    {
        return (!hasUnreviewedResults && unexported.Count == 0) || (confirmDiscard?.Invoke() ?? MessageBox.Show(this, "This run may have result files that have not been exported. Discard these temporary results?", "Try Run", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.OK);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (cancellation is not null)
        {
            windowBorders?.Dispose();
            e.Cancel = true;
            closeWhenStopped = true;
            cancellation.Cancel();
            return;
        }

        if (!ConfirmDiscard())
        {
            e.Cancel = true;
            return;
        }

        try
        {
            session?.Dispose();
        }
        catch (IOException exception)
        {
            MessageBox.Show(this, "Temporary files could not be removed: " + exception.Message, "Try Run", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (UnauthorizedAccessException exception)
        {
            MessageBox.Show(this, "Temporary files could not be removed: " + exception.Message, "Try Run", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
