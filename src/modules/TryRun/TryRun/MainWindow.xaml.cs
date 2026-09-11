// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Windows;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF owns the window lifetime. Closing cancels active work and disposes the session; the run's finally block disposes its cancellation source.")]
public partial class MainWindow : Window
{
    private readonly string workerPath = Path.Combine(AppContext.BaseDirectory, "Worker", "PowerToys.TryRun.Worker.exe");
    private CancellationTokenSource? cancellation;
    private RunSession? session;
    private bool closeWhenStopped;
    private bool canProbe;

    public MainWindow()
    {
        InitializeComponent();
        var reason = RuntimeRequirements.GetUnavailableReason();
        if (reason is null && !File.Exists(workerPath))
        {
            reason = "Build the MXC execution worker before running scripts. See src/modules/TryRun/README.md.";
        }

        if (reason is not null)
        {
            StatusText.Text = reason;
            RunButton.IsEnabled = false;
        }
        else
        {
            canProbe = true;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!canProbe)
        {
            return;
        }

        canProbe = false;
        cancellation = new CancellationTokenSource();
        SetRunning(true);
        StatusText.Text = "Checking restricted workspace access…";
        var available = false;
        try
        {
            var failure = await new WorkerClient(workerPath).GetAvailabilityFailureAsync(cancellation.Token);
            available = failure is null;
            StatusText.Text = failure ?? "Ready. Scripts run only when you choose Run.";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Availability check stopped. Reopen Try Run to check again.";
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
        }
        finally
        {
            cancellation.Dispose();
            cancellation = null;
            SetRunning(false);
            RunButton.IsEnabled = available;
            if (closeWhenStopped)
            {
                Close();
            }
        }
    }

    private async void OnRun(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TimeoutBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var timeout) || timeout is < 1 or > 300)
        {
            StatusText.Text = "Choose a timeout between 1 and 300 seconds.";
            return;
        }

        cancellation = new CancellationTokenSource();
        SetRunning(true);
        var buffer = new OutputBuffer();
        OutputBox.Clear();
        StatusText.Text = "Starting a restricted run…";
        try
        {
            session?.Dispose();
            session = new RunSession();
            var request = new ExecutionRequest(ScriptBox.Text, session.WorkingDirectory, session.TemporaryDirectory, timeout);
            var progress = new Progress<WorkerMessage>(message =>
            {
                if (message.Kind is WorkerMessage.Output or WorkerMessage.Error)
                {
                    buffer.Append(message.Text);
                    OutputBox.Text = buffer.ToString();
                    OutputBox.ScrollToEnd();
                }
            });
            var result = await new WorkerClient(workerPath).RunAsync(request, progress, cancellation.Token);
            StatusText.Text = result.TimedOut ? "Stopped: time limit reached." : result.ExitCode == 0 ? "Finished. Exit code: 0." : $"Run failed. Exit code: {result.ExitCode}. See the output for details.";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Stopped.";
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
        }
        finally
        {
            cancellation.Dispose();
            cancellation = null;
            SetRunning(false);
            if (closeWhenStopped)
            {
                Close();
            }
        }
    }

    private void OnStop(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Stopping the run…";
        StopButton.IsEnabled = false;
        cancellation?.Cancel();
    }

    private void SetRunning(bool running)
    {
        RunButton.IsEnabled = !running;
        StopButton.IsEnabled = running;
        ScriptBox.IsEnabled = !running;
        TimeoutBox.IsEnabled = !running;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (cancellation is not null)
        {
            e.Cancel = true;
            closeWhenStopped = true;
            cancellation.Cancel();
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
