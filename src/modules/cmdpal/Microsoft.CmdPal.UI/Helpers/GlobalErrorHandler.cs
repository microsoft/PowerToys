// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using ManagedCommon;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using SystemUnhandledExceptionEventArgs = System.UnhandledExceptionEventArgs;
using XamlUnhandledExceptionEventArgs = Microsoft.UI.Xaml.UnhandledExceptionEventArgs;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class GlobalErrorHandler
{
    internal static void Register(App app)
    {
        app.UnhandledException += App_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private static void App_UnhandledException(object sender, XamlUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception, "UI Thread Exception");

        if (IsDisposalException(e.Exception))
        {
            Debug.Assert(false, $"Exception {e.Exception.Message} is handled silently in non-Debug build configurations", e.Exception.ToString());
            e.Handled = true;
            return;
        }

        e.Handled = TryShowErrorDialog(e.Exception);
    }

    private static void CurrentDomain_UnhandledException(object sender, SystemUnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex, "Background Thread Exception");
        }
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException(e.Exception, "Unobserved Task Exception");
        e.SetObserved();
    }

    private static bool IsDisposalException(Exception ex)
    {
        return ex is ObjectDisposedException ||
               (ex is InvalidOperationException disposalEx &&
               (disposalEx.Message.Contains("disposed") ||
                disposalEx.Message.Contains("closed")));
    }

    private static bool TryShowErrorDialog(Exception ex)
    {
        try
        {
            if (App.Current.AppWindow?.Content != null)
            {
                var dialog = new ContentDialog
                {
                    Title = "Unexpected Error",
                    Content = $"An error occurred: {ex.Message}",
                    CloseButtonText = "Close",
                    PrimaryButtonText = "Save to File",
                    SecondaryButtonText = "Copy to Clipboard",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = App.Current.AppWindow.Content.XamlRoot,
                    FullSizeDesired = true,
                };

                _ = dialog.ShowAsync().AsTask().ContinueWith(task =>
                {
                    if (task is { IsCompletedSuccessfully: true, Result: ContentDialogResult.Primary })
                    {
                        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        var fileName = $"CmdPal_Error_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.txt";
                        var filePath = Path.Combine(desktopPath, fileName);

                        File.WriteAllText(filePath, ex.ToString());

                        try
                        {
                            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                        }
                        catch (Exception fileOpenException)
                        {
                            LogException(fileOpenException, "Failed to open error file");
                        }

                        var package = new DataPackage();
                        package.SetText($"Error details saved to: {filePath}");
                        Clipboard.SetContent(package);
                    }
                    else if (task is { IsCompletedSuccessfully: true, Result: ContentDialogResult.Secondary })
                    {
                        var package = new DataPackage();
                        package.SetText(ex.ToString());
                        Clipboard.SetContent(package);
                    }
                });

                return true;
            }
        }
        catch (Exception errorDialogException)
        {
            LogException(errorDialogException, "Failed to show error dialog");
        }

        return false;
    }

    private static void LogException(Exception ex, string context)
    {
        Logger.LogError($"{context}\n", ex);
    }
}
