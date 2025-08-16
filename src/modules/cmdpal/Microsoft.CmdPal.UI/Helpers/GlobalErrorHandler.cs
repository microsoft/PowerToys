// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

using ManagedCommon;

using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

using SystemUnhandledExceptionEventArgs = System.UnhandledExceptionEventArgs;
using XamlUnhandledExceptionEventArgs = Microsoft.UI.Xaml.UnhandledExceptionEventArgs;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Global error handler for Command Palette.
/// </summary>
internal sealed class GlobalErrorHandler : IDisposable
{
    private readonly ErrorReportWindowManager _errorReportWindowManager = new();
    private readonly ErrorReportBuilder _errorReportBuilder = new();
    private Options? _options;

    internal void Register(App app, Options options)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(options);

        _options = options;

        app.UnhandledException += App_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_UnhandledException(object sender, XamlUnhandledExceptionEventArgs e)
    {
        // Exceptions thrown on the main UI thread are handled here.
        e.Handled = true;
        HandleException(e.Exception, Context.MainThreadException, true);
    }

    private void CurrentDomain_UnhandledException(object sender, SystemUnhandledExceptionEventArgs e)
    {
        // Exceptions thrown on background threads are handled here.
        if (e.ExceptionObject is Exception ex)
        {
            HandleException(ex, Context.AppDomainUnhandledException);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // This event is raised only when a faulted Task is garbage-collected
        // without its exception being observed. It is NOT raised immediately
        // when the Task faults; timing depends on GC finalization.
        e.SetObserved();
        HandleException(e.Exception, Context.UnobservedTaskException);
    }

    private static bool IsDisposalException(Exception ex)
    {
        return ex is ObjectDisposedException ||
               (ex is InvalidOperationException disposalEx &&
               (disposalEx.Message.Contains("disposed") ||
                disposalEx.Message.Contains("closed")));
    }

    private static bool IsDwmCompositionChangedException(Exception ex)
    {
        return ex is COMException && ex.StackTrace?.Contains("DwmCompositionChanged") == true;
    }

    private void TryShowErrorDialog(Exception exception, string? errorReport = null, string? errorReportFile = null, bool recoverable = false)
    {
        recoverable &= _options!.IsRecoverableException(exception);

        _errorReportWindowManager.Show(new ErrorReportWindow.Options
        {
            DisableCloseButton = !recoverable,
            ErrorReport = errorReport ?? exception.ToString(),
            ReportFilePath = errorReportFile,
            Mode = recoverable ? ErrorReportWindow.TroubleMode.Fatal : ErrorReportWindow.TroubleMode.Recoverable,
        });
    }

    private static string? StoreReport(string report)
    {
        try
        {
            // Annoyance level: mild... litter user desktop with exception reports
            var logDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var reportFilePath = Path.Combine(logDirectory, $"CmdPal_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(reportFilePath, report);
            return reportFilePath;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to store exception report", ex);
            return null;
        }
    }

    private void HandleException(Exception ex, Context context, bool isRecoverable = false)
    {
        var reportActions = GetReportActionsForContext(context);

        if (reportActions == LogTypes.None || !_options!.ShouldHandleException(ex))
        {
            return;
        }

        if (reportActions.HasFlag(LogTypes.WriteToLog))
        {
            LogException(ex, context);
        }

        var reportContent = _errorReportBuilder.BuildReport(ex, context.ToString(), _options.ScrubPersonalInformation);

        string? reportPath = null;
        if (reportActions.HasFlag(LogTypes.WriteToFile))
        {
            reportPath = StoreReport(reportContent);
        }

        var isTonedDown = IsDwmCompositionChangedException(ex);

        if (isTonedDown || (reportActions.HasFlag(LogTypes.ShowNotification) && !_options.IsSilentException(ex)))
        {
            PushNotification(ex, reportPath);
        }

        if (!isTonedDown && reportActions.HasFlag(LogTypes.ShowDialog) && !_options.IsSilentException(ex))
        {
            TryShowErrorDialog(ex, reportContent, reportPath, isRecoverable);
        }
    }

    private static void LogException(Exception ex, Context context)
    {
        Logger.LogError($"{context}", ex);
    }

    private static void PushNotification(Exception ex, string? errorReportFile)
    {
        // Reopen button will start the application if it was closed.
        // If the application is already running, then it will bring the main window to the foreground.
        var hasErrorReport = !string.IsNullOrWhiteSpace(errorReportFile) && File.Exists(errorReportFile);

        var notificationBuilder = new AppNotificationBuilder()
                .SetTag("LastCmdPalException")
                .AddText("An unrecoverable error occurred");

        if (hasErrorReport)
        {
            notificationBuilder
                .AddText("Error report was saved to your desktop.");
        }

        notificationBuilder
            .AddButton(new AppNotificationButton("Reopen") { ButtonStyle = AppNotificationButtonStyle.Default });

        if (hasErrorReport)
        {
            notificationBuilder
                .AddButton(new AppNotificationButton("Open Report")
                {
                    ButtonStyle = AppNotificationButtonStyle.Default,
                    InvokeUri = new Uri($"file:///{Uri.EscapeDataString(errorReportFile!)}"),
                });
        }

        var notification = notificationBuilder.BuildNotification();

        AppNotificationManager.Default.Show(notification);
    }

    private LogTypes GetReportActionsForContext(Context context)
    {
        return context switch
        {
            Context.MainThreadException => _options!.MainThreadExceptionHandling,
            Context.BackgroundThreadException => _options!.MainThreadExceptionHandling,
            Context.UnobservedTaskException => _options!.UnobservedTaskExceptionHandling,
            Context.AppDomainUnhandledException => _options!.AppDomainUnhandledExceptionHandling,
            _ => LogTypes.None,
        };
    }

    public void Dispose()
    {
        App.Current.UnhandledException -= App_UnhandledException;
        TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
    }

    internal sealed class Options
    {
        public static Options Default { get; } = new();

        public LogTypes MainThreadExceptionHandling { get; init; } = LogTypes.WriteToFile | LogTypes.WriteToLog | LogTypes.ShowDialog;

        public LogTypes UnobservedTaskExceptionHandling { get; init; } = LogTypes.WriteToLog | LogTypes.ShowNotification;

        public LogTypes AppDomainUnhandledExceptionHandling { get; init; } = LogTypes.WriteToLog | LogTypes.ShowNotification;

        public Func<Exception, bool> ShouldHandleException { get; init; } = ex =>
            !IsDwmCompositionChangedException(ex) &&
            !IsDisposalException(ex);

        /// <summary>
        /// Determines if the exception is recoverable and the application can continue running.
        /// </summary>
        public Func<Exception, bool> IsRecoverableException { get; init; } = static _ => false;

        /// <summary>
        /// Determines if the exception should be silently logged without user notification.
        /// </summary>
        public Func<Exception, bool> IsSilentException { get; init; } = static _ => false;

        public bool ScrubPersonalInformation { get; init; } = true;
    }

    [Flags]
    internal enum LogTypes
    {
        None = 0x0,
        WriteToLog = 0x1,
        ShowNotification = 0x2,
        ShowDialog = 0x4,
        WriteToFile = 0x8,
    }

    private enum Context
    {
        MainThreadException,
        BackgroundThreadException,
        UnobservedTaskException,
        AppDomainUnhandledException,
    }
}
