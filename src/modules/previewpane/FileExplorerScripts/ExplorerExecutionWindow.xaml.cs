// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileExplorerScripts.Core;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;

namespace FileExplorerScripts;

public sealed partial class ExplorerExecutionWindow : Window
{
    private readonly CancellationTokenSource? _cancellation;
    private readonly DispatcherTimer _popupTimer = new();
    private readonly DispatcherTimer _elapsedTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Stopwatch _elapsed = new();
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _running;
    private ExplorerScriptStage _stage;

    internal ExplorerExecutionWindow(string name, int fileCount, CancellationTokenSource? cancellation = null)
    {
        _cancellation = cancellation;
        InitializeComponent();
        Title = Strings.Get("Title");
        Heading.Text = name;
        Summary.Text = fileCount > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.SelectionSummary, fileCount) : string.Empty;
        CancellationNotice.Text = Strings.Get("CancellationNotice");
        DetailsExpander.Header = Strings.Get("ErrorDetails");
        CopyButton.Content = Strings.Get("CopyDetails");
        CancelButton.Content = Strings.Get("Cancel");
        AppWindow.Resize(new SizeInt32(640, 480));
        AppWindow.Closing += (_, e) =>
        {
            if (_running)
            {
                e.Cancel = true;
                AppWindow.Hide();
            }
        };
        Closed += (_, _) =>
        {
            Finish();
            _closed.TrySetResult();
        };
        _popupTimer.Tick += (_, _) =>
        {
            _popupTimer.Stop();
            if (_running)
            {
                AppWindow.Show(false);
            }
        };
        _elapsedTimer.Tick += (_, _) => UpdateElapsed();
    }

    internal bool WasCancelled { get; private set; }

    internal void Start(TimeSpan delay)
    {
        _running = true;
        Progress.IsActive = true;
        _elapsed.Start();
        UpdateElapsed();
        _elapsedTimer.Start();
        ReportStage(ExplorerScriptStage.Preparing);
        if (delay == TimeSpan.Zero)
        {
            AppWindow.Show(false);
        }
        else
        {
            _popupTimer.Interval = delay;
            _popupTimer.Start();
        }
    }

    internal void ReportStage(ExplorerScriptStage stage)
    {
        if (!_running || WasCancelled)
        {
            return;
        }

        _stage = stage;
        Stage.Text = Strings.Get(stage switch
        {
            ExplorerScriptStage.Preparing => "Preparing",
            ExplorerScriptStage.Running => "Running",
            ExplorerScriptStage.Saving => "Saving",
            _ => throw new ArgumentOutOfRangeException(nameof(stage)),
        });
    }

    internal void Finish()
    {
        _running = false;
        _popupTimer.Stop();
        _elapsedTimer.Stop();
        _elapsed.Stop();
        Progress.IsActive = false;
    }

    internal Task ShowFailureAsync(Exception exception)
    {
        Finish();
        ProgressRow.Visibility = Visibility.Collapsed;
        Failure.Title = Strings.Get(WasCancelled ? "CancelledTitle" : _stage == ExplorerScriptStage.Saving ? "SaveFailed" : "Failed");
        Failure.Message = exception.Message;
        Failure.Severity = WasCancelled ? InfoBarSeverity.Warning : InfoBarSeverity.Error;
        Failure.IsOpen = true;
        CancellationNotice.Text = Strings.Get("FailureNotice");
        var details = new StringBuilder(exception.ToString());
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Data["ExportedPaths"] is IEnumerable<string> paths)
            {
                details.AppendLine().AppendLine(Strings.Get("RetainedOutputs"));
                foreach (var path in paths)
                {
                    details.AppendLine(path);
                }
            }

            if (current.Data["TemporaryPath"] is string temporaryPath)
            {
                details.AppendLine().AppendLine(temporaryPath);
                details.AppendLine(current.Data["CleanupError"]?.ToString());
            }
        }

        Details.Text = details.ToString();
        DetailsExpander.Visibility = Visibility.Visible;
        CopyButton.Visibility = Visibility.Visible;
        CancelButton.Content = Strings.Get("Close");
        CancelButton.IsEnabled = true;
        Activate();
        return _closed.Task;
    }

    private void UpdateElapsed() =>
        Elapsed.Text = string.Format(CultureInfo.CurrentCulture, Strings.Elapsed, _elapsed.Elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture));

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_running)
        {
            Close();
            return;
        }

        WasCancelled = true;
        Stage.Text = Strings.Get("Cancelling");
        CancelButton.IsEnabled = false;
        _cancellation?.Cancel();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var data = new DataPackage();
            data.SetText(Heading.Text + Environment.NewLine + Failure.Title + Environment.NewLine + Details.Text);
            Clipboard.SetContent(data);
            Clipboard.Flush();
        }
        catch (COMException exception)
        {
            Logger.LogError("Unable to copy the Explorer PowerScripts error details.", exception);
            Failure.Message = Strings.Get("CopyFailed");
        }
    }
}
