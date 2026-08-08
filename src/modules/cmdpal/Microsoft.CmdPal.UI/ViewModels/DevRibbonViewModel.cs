// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.UI;
using Windows.System;
using Windows.UI;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed partial class DevRibbonViewModel : ObservableObject
{
    private const int MaxLogEntries = 2;
    private const string Release = "Release";
    private const string Debug = "Debug";

    private static readonly Color ReleaseAotColor = ColorHelper.FromArgb(255, 124, 58, 237);
    private static readonly Color ReleaseColor = ColorHelper.FromArgb(255, 51, 65, 85);
    private static readonly Color DebugAotColor = ColorHelper.FromArgb(255, 99, 102, 241);
    private static readonly Color DebugColor = ColorHelper.FromArgb(255, 107, 114, 128);

    private readonly DispatcherQueue _dispatcherQueue;

    public DevRibbonViewModel()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        Trace.Listeners.Add(new DevRibbonTraceListener(this));

        var configLabel = BuildConfiguration == Release ? "RLS" : "DBG"; /* #no-spell-check-line */
        var aotLabel = BuildInfo.IsNativeAot ? "⚡AOT" : "NO AOT";
        Tag = $"{configLabel} | {aotLabel}";

        TagColor = (BuildConfiguration, BuildInfo.IsNativeAot) switch
        {
            (Release, true) => ReleaseAotColor,
            (Release, false) => ReleaseColor,
            (Debug, true) => DebugAotColor,
            (Debug, false) => DebugColor,
            _ => Colors.Fuchsia,
        };
    }

    public string BuildConfiguration => BuildInfo.Configuration;

    public bool IsAotReleaseConfiguration => BuildConfiguration == Release && BuildInfo.IsNativeAot;

    public bool IsAot => BuildInfo.IsNativeAot;

    public bool IsPublishTrimmed => BuildInfo.PublishTrimmed;

    public ObservableCollection<LogEntryViewModel> LatestLogs { get; } = [];

    [ObservableProperty]
    public partial int WarningCount { get; private set; }

    [ObservableProperty]
    public partial int ErrorCount { get; private set; }

    [ObservableProperty]
    public partial string Tag { get; private set; }

    [ObservableProperty]
    public partial Color TagColor { get; private set; }

    [ObservableProperty]
    public partial bool IsInstanceTrackingEnabled { get; private set; }

    [ObservableProperty]
    public partial string MemorySummary { get; private set; } = "Not measured yet.";

    [ObservableProperty]
    public partial string LiveViewModels { get; private set; } = "Enable tracking, then open and leave a page.";

    [RelayCommand]
    private void ToggleInstanceTracking()
    {
        ViewModelInstanceTracker.IsEnabled = !ViewModelInstanceTracker.IsEnabled;
        IsInstanceTrackingEnabled = ViewModelInstanceTracker.IsEnabled;

        if (!IsInstanceTrackingEnabled)
        {
            ViewModelInstanceTracker.Reset();
        }

        RefreshMemory();
    }

    /// <summary>
    /// Runs a full collect / drain / collect cycle before reading the counters.
    /// Without the drain, view-models whose extension proxies are merely queued
    /// for finalization still read as alive.
    /// </summary>
    /// <remarks>
    /// The collection deliberately runs off the UI thread. Releasing an
    /// apartment-bound RCW marshals back to the thread that created it, so a UI
    /// thread parked inside WaitForPendingFinalizers is the one thread those
    /// releases cannot make progress on - the proxies that most need draining
    /// are exactly the ones that would never drain.
    /// </remarks>
    [RelayCommand]
    private async Task CollectAndMeasureAsync()
    {
        await Task.Run(ViewModelInstanceTracker.ForceFullCollection).ConfigureAwait(true);
        RefreshMemory();
    }

    [RelayCommand]
    private void ResetInstanceTracking()
    {
        ViewModelInstanceTracker.Reset();
        RefreshMemory();
    }

    [RelayCommand]
    private void RefreshMemory()
    {
        var heapMb = GC.GetTotalMemory(forceFullCollection: false) / (1024d * 1024d);
        var workingSetMb = Environment.WorkingSet / (1024d * 1024d);

        MemorySummary = $"Heap {heapMb:N1} MB   |   Working set {workingSetMb:N1} MB   |   Gen2 collections {GC.CollectionCount(2):N0}";

        if (!ViewModelInstanceTracker.IsEnabled)
        {
            LiveViewModels = "Tracking is off. Turn it on, then open and leave a page.";
            return;
        }

        var snapshot = ViewModelInstanceTracker.Snapshot();
        if (snapshot.Count == 0)
        {
            LiveViewModels = "Nothing recorded yet - only view-models created while tracking is on are counted.";
            return;
        }

        LiveViewModels = string.Join(
            Environment.NewLine,
            snapshot.Select(static entry => $"{entry.TypeName}: {entry.Alive:N0} alive of {entry.Seen:N0} created, {entry.CleanedUp:N0} cleaned up"));
    }

    [RelayCommand]
    private async Task OpenLogFileAsync()
    {
        var logPath = Logger.CurrentLogFile;
        if (File.Exists(logPath))
        {
            await Launcher.LaunchUriAsync(new Uri(logPath));
        }
    }

    [RelayCommand]
    private async Task OpenLogFolderAsync()
    {
        var logFolderPath = Logger.CurrentVersionLogDirectoryPath;
        if (Directory.Exists(logFolderPath))
        {
            await Launcher.LaunchFolderPathAsync(logFolderPath);
        }
    }

    [RelayCommand]
    private void ResetErrorCounters()
    {
        WarningCount = 0;
        ErrorCount = 0;
        LatestLogs.Clear();
    }

    [RelayCommand]
    private void OpenInternalTools()
    {
        WeakReferenceMessenger.Default.Send(new OpenSettingsMessage("Internal"));
    }

    [RelayCommand]
    private void ToggleDevRibbonVisibility()
    {
        WeakReferenceMessenger.Default.Send(new ToggleDevRibbonMessage());
    }

    private sealed partial class DevRibbonTraceListener(DevRibbonViewModel viewModel) : TraceListener
    {
        private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

        [GeneratedRegex(@"^\[(?<timestamp>.*?)\] \[(?<severity>.*?)\] (?<message>.*)")]
        private static partial Regex LogRegex();

        private readonly Lock _lock = new();
        private LogEntryViewModel? _latestLogEntry;

        public override void Write(string? message)
        {
            // Not required for this scenario.
        }

        public override void WriteLine(string? message)
        {
            if (message is null)
            {
                return;
            }

            lock (_lock)
            {
                var match = LogRegex().Match(message);
                if (match.Success)
                {
                    var severity = match.Groups["severity"].Value;
                    var isWarning = severity.Equals("Warning", StringComparison.OrdinalIgnoreCase);
                    var isError = severity.Equals("Error", StringComparison.OrdinalIgnoreCase);

                    if (isWarning || isError)
                    {
                        var timestampStr = match.Groups["timestamp"].Value;
                        var timestamp = DateTimeOffset.TryParseExact(
                            timestampStr,
                            TimestampFormat,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeLocal,
                            out var parsed)
                            ? parsed
                            : DateTimeOffset.Now;

                        var logEntry = new LogEntryViewModel(
                            timestamp,
                            severity,
                            match.Groups["message"].Value,
                            string.Empty);

                        _latestLogEntry = logEntry;

                        viewModel._dispatcherQueue.TryEnqueue(() =>
                        {
                            if (isWarning)
                            {
                                viewModel.WarningCount++;
                            }
                            else
                            {
                                viewModel.ErrorCount++;
                            }

                            viewModel.LatestLogs.Insert(0, logEntry);

                            while (viewModel.LatestLogs.Count > MaxLogEntries)
                            {
                                viewModel.LatestLogs.RemoveAt(viewModel.LatestLogs.Count - 1);
                            }
                        });
                    }
                    else
                    {
                        _latestLogEntry = null;
                    }

                    return;
                }

                if (IndentLevel > 0 && _latestLogEntry is { } latest)
                {
                    viewModel._dispatcherQueue.TryEnqueue(() =>
                    {
                        latest.AppendDetails(message);
                    });
                }
            }
        }
    }
}
