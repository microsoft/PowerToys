// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.UI.Xaml;
using Windows.System;
using Microsoft.Extensions.DependencyInjection;
using Page = Microsoft.UI.Xaml.Controls.Page;

namespace Microsoft.CmdPal.UI.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class InternalPage : Page
{
    private readonly IApplicationInfoService _appInfoService;

    public InternalPage()
    {
        InitializeComponent();

        _appInfoService = App.Current.Services.GetRequiredService<IApplicationInfoService>();
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
}
