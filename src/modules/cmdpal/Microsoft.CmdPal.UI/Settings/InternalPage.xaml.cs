// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using Page = Microsoft.UI.Xaml.Controls.Page;

namespace Microsoft.CmdPal.UI.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class InternalPage : Page
{
    private readonly IApplicationInfoService _appInfoService;
    private readonly ISettingsService _settingsService;

    internal ObservableCollection<IconDiagnosticsReportItem> IconDiagnosticReports { get; } = [];

    public string GalleryFeedUrl => _settingsService.Settings.GalleryFeedUrl ?? string.Empty;

    public bool ShowHwndFrame => _settingsService.Settings.ShowHwndFrame;

    public InternalPage()
    {
        InitializeComponent();

        _appInfoService = App.Current.Services.GetRequiredService<IApplicationInfoService>();
        _settingsService = App.Current.Services.GetRequiredService<ISettingsService>();
        LoadIconDiagnosticsReports();
        UpdateIconDiagnosticsControls();
    }

    private void GalleryFeedUrlTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            var newUrl = string.IsNullOrWhiteSpace(textBox.Text) ? null : textBox.Text.Trim();
            if (newUrl != _settingsService.Settings.GalleryFeedUrl)
            {
                _settingsService.UpdateSettings(s => s with { GalleryFeedUrl = newUrl });
            }
        }
    }

    private void ThrowPlainMainThreadException_Click(object sender, RoutedEventArgs e)
    {
        Logger.LogDebug("Throwing test exception from the UI thread");
        throw new NotImplementedException("Test exception; thrown from the UI thread");
    }

    private void ThrowExceptionInUnobservedTask_Click(object sender, RoutedEventArgs e)
    {
        Logger.LogDebug("Starting a task that will throw test exception");
        Task.Run(() =>
        {
            Logger.LogDebug("Throwing test exception from a task");
            throw new InvalidOperationException("Test exception; thrown from a task");
        });
    }

    private void ThrowPlainMainThreadExceptionPii_Click(object sender, RoutedEventArgs e)
    {
        Logger.LogDebug("Throwing test exception from the UI thread (PII)");
        throw new InvalidOperationException(SampleData.ExceptionMessageWithPii);
    }

    private async void OpenLogsCardClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var logFolderPath = _appInfoService.LogDirectory;
            if (Directory.Exists(logFolderPath))
            {
                await Launcher.LaunchFolderPathAsync(logFolderPath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to open directory in Explorer", ex);
        }
    }

    private async void OpenCurrentLogCardClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var logPath = Logger.CurrentLogFile;
            if (File.Exists(logPath))
            {
                await Launcher.LaunchUriAsync(new Uri(logPath));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to open log file", ex);
        }
    }

    private void StartIconDiagnosticsClicked(object sender, RoutedEventArgs e)
    {
        var sessionId = IconLoadDiagnostics.Start(DispatcherQueue);
        IconDiagnosticsStatusTextBlock.Text = $"Recording session {sessionId}. Reproduce the icon workload, then select Stop.";
        UpdateIconDiagnosticsControls();
    }

    private void StopIconDiagnosticsClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var report = IconLoadDiagnostics.StopAndCreateReport();
            if (report is null)
            {
                IconDiagnosticsStatusTextBlock.Text = "No icon diagnostics session is recording.";
            }
            else
            {
                IconDiagnosticReports.Insert(0, new IconDiagnosticsReportItem(report));
                IconDiagnosticsExpander.IsExpanded = true;
                IconDiagnosticsStatusTextBlock.Text = $"Session {report.SessionId} stopped. Its report was written to the current log and added below.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to stop icon diagnostics", ex);
            IconDiagnosticsStatusTextBlock.Text = "The icon diagnostics session could not be stopped.";
        }

        UpdateIconDiagnosticsControls();
    }

    private void ResetIconDiagnosticsClicked(object sender, RoutedEventArgs e)
    {
        IconLoadDiagnostics.Reset();
        IconDiagnosticReports.Clear();
        IconDiagnosticsStatusTextBlock.Text = "Icon diagnostics were reset.";
        UpdateIconDiagnosticsControls();
    }

    private void CopyIconDiagnosticsReportClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: IconLoadDiagnosticsReport report })
        {
            return;
        }

        try
        {
            ClipboardHelper.SetText(report.Text);
            IconDiagnosticsStatusTextBlock.Text = $"Session {report.SessionId} report copied to the clipboard.";
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to copy icon diagnostics", ex);
            IconDiagnosticsStatusTextBlock.Text = $"Session {report.SessionId} report could not be copied to the clipboard.";
        }
    }

    private void LoadIconDiagnosticsReports()
    {
        var reports = IconLoadDiagnostics.GetReports();
        for (var i = reports.Count - 1; i >= 0; i--)
        {
            IconDiagnosticReports.Add(new IconDiagnosticsReportItem(reports[i]));
        }

        if (IconDiagnosticReports.Count > 0)
        {
            IconDiagnosticsStatusTextBlock.Text = $"{IconDiagnosticReports.Count} report{(IconDiagnosticReports.Count == 1 ? string.Empty : "s")} available below.";
        }
    }

    private void UpdateIconDiagnosticsControls()
    {
        var isRecording = IconLoadDiagnostics.IsRecording;
        StartIconDiagnosticsButton.IsEnabled = !isRecording;
        StopIconDiagnosticsButton.IsEnabled = isRecording;

        if (isRecording && IconLoadDiagnostics.ActiveSessionId is { } sessionId)
        {
            IconDiagnosticsStatusTextBlock.Text = $"Recording session {sessionId}. Reproduce the icon workload, then select Stop.";
        }
    }

    private async void OpenConfigFolderCardClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var directory = _appInfoService.ConfigDirectory;
            if (Directory.Exists(directory))
            {
                await Launcher.LaunchFolderPathAsync(directory);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to open directory in Explorer", ex);
        }
    }

    private void ToggleDevRibbonClicked(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new ToggleDevRibbonMessage());
    }

    private void ShowHwndFrameToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            var newValue = toggle.IsOn;
            if (newValue != _settingsService.Settings.ShowHwndFrame)
            {
                _settingsService.UpdateSettings(s => s with { ShowHwndFrame = newValue });
            }
        }
    }
}
