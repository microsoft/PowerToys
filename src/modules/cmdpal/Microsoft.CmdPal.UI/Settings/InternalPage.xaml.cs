// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class InternalPage : Page
{
    public InternalPage()
    {
        InitializeComponent();
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
            throw new InvalidOperationException("Test exception; throw from a task");
        });
    }

    private void ThrowPlainMainThreadExceptionPii_Click(object sender, RoutedEventArgs e)
    {
        Logger.LogDebug("Throwing test exception from the UI thread (PII)");
        throw new InvalidOperationException(SampleData.ExceptionMessageWithPii);
    }

    private void OpenLogsCardClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var logDirPath = Logger.CurrentVersionLogDirectoryPath;
            if (!string.IsNullOrWhiteSpace(logDirPath) && Directory.Exists(logDirPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = logDirPath,
                    UseShellExecute = true,
                });
            }
            else
            {
                PInvoke.MessageBox(HWND.Null, $"Can't find the log directory: {logDirPath}", "Error", MESSAGEBOX_STYLE.MB_OK);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to open directory in Explorer", ex);
        }
    }

    private void OpenCurrentLogCardClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var logFile = Logger.CurrentLogFile;
            if (!File.Exists(logFile))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = logFile,
                    UseShellExecute = true,
                });
            }
            else
            {
                PInvoke.MessageBox(HWND.Null, $"Can't find the log file: {logFile}", "Error", MESSAGEBOX_STYLE.MB_OK);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to open log file", ex);
        }
    }
}
