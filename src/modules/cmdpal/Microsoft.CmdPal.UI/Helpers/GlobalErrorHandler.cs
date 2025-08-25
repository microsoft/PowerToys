// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services.Reports;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

using SystemUnhandledExceptionEventArgs = System.UnhandledExceptionEventArgs;
using XamlUnhandledExceptionEventArgs = Microsoft.UI.Xaml.UnhandledExceptionEventArgs;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Global error handler for Command Palette.
/// </summary>
internal sealed partial class GlobalErrorHandler : IDisposable
{
    // GlobalErrorHandler is designed to be self-contained; it can be registered and invoked before a service provider is available.
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
        HandleException(e.Exception, Context.UnobservedTaskException, isRecoverable: true);
    }

    private void TryShowErrorDialog(Exception exception, string? errorReport = null, string? errorReportFile = null, bool recoverable = false)
    {
        recoverable &= _options!.IsRecoverableException(exception);

        _errorReportWindowManager.Show(new ErrorReportWindow.Options
        {
            DisableCloseButton = !recoverable,
            ErrorReport = errorReport ?? exception.ToString(),
            ReportFilePath = errorReportFile,
            Mode = recoverable ? ErrorReportWindow.TroubleMode.Recoverable : ErrorReportWindow.TroubleMode.Fatal,
        });
    }

    private void HandleException(Exception ex, Context context, bool isRecoverable = false)
    {
        var handlingOptions = GetReportActionsForContext(context);

        if (handlingOptions == ExceptionHandlingOptions.None || !_options!.ShouldHandleException(ex))
        {
            return;
        }

        if (handlingOptions.HasFlag(ExceptionHandlingOptions.WriteToLog))
        {
            LogException(ex, context);
        }

        var reportContent = _errorReportBuilder.BuildReport(ex, context.ToString(), _options.RedactPii);

        string? reportPath = null;
        if (handlingOptions.HasFlag(ExceptionHandlingOptions.CreateReport))
        {
            reportPath = StoreReport(reportContent, _options.StoreReportOnUserDesktop);
        }

        var isTonedDown = IsDwmCompositionChangedException(ex);

        if (isTonedDown || (handlingOptions.HasFlag(ExceptionHandlingOptions.ShowNotification) && !_options.IsSilentException(ex)))
        {
            PushNotification(reportPath, isRecoverable);
        }

        if (!isTonedDown && handlingOptions.HasFlag(ExceptionHandlingOptions.ShowDialog) && !_options.IsSilentException(ex))
        {
            TryShowErrorDialog(ex, reportContent, reportPath, isRecoverable);
        }
    }

    private static void LogException(Exception ex, Context context)
    {
        Logger.LogError($"{context}", ex);
    }

    private static string? StoreReport(string report, bool storeOnDesktop)
    {
        // Generate a unique name for the report file; include timestamp and a random zero-padded number to avoid collisions
        // in case of crash storm.
        var name = FormattableString.Invariant($"CmdPal_ErrorReport_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{Random.Shared.Next(100000):D5}.log");

        // Always store a copy in log directory, this way it is available for Bug Report Tool
        var reportPath = Save(report, name, () => Logger.CurrentVersionLogDirectoryPath);

        // Optionally store a copy on the desktop for user (in)convenience
        if (storeOnDesktop)
        {
            var path = Save(report, name, () => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));

            // show the desktop copy if both succeeded
            if (path != null)
            {
                reportPath = path;
            }
        }

        return reportPath;

        static string? Save(string reportContent, string reportFileName, Func<string> directory)
        {
            try
            {
                var logDirectory = directory();
                Directory.CreateDirectory(logDirectory);
                var reportFilePath = Path.Combine(logDirectory, reportFileName);
                File.WriteAllText(reportFilePath, reportContent);
                return reportFilePath;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to store exception report", ex);
                return null;
            }
        }
    }

    private static void PushNotification(string? errorReportFile, bool isRecoverable)
    {
        // Reopen button will start the application if it was closed.
        // If the application is already running, then it will bring the main window to the foreground.
        var hasErrorReport = !string.IsNullOrWhiteSpace(errorReportFile) && File.Exists(errorReportFile);

        // get the summary text from resources:
        var summary = isRecoverable
            ? ResourceLoaderInstance.GetString("ErrorReport_Notification_Summary_Recoverable")
            : ResourceLoaderInstance.GetString("ErrorReport_Notification_Summary_Unrecoverable");
        var notificationBuilder = new AppNotificationBuilder()
                .SetTag("LastCmdPalException")
                .AddText(summary);

        if (hasErrorReport)
        {
            notificationBuilder.AddText(ResourceLoaderInstance.GetString("ErrorReport_Notification_Body_ErrorReportWasSaved"));
        }

        var reopenButtonText = ResourceLoaderInstance.GetString("ErrorReport_Notification_ReopenButton_Text");
        notificationBuilder.AddButton(new AppNotificationButton(reopenButtonText) { ButtonStyle = AppNotificationButtonStyle.Default });

        if (hasErrorReport)
        {
            var openReportButtonText = ResourceLoaderInstance.GetString("ErrorReport_Notification_OpenReportButton_Text");
            notificationBuilder.AddButton(new AppNotificationButton(openReportButtonText)
            {
                ButtonStyle = AppNotificationButtonStyle.Default,
                InvokeUri = new Uri(errorReportFile!),
            });
        }

        var notification = notificationBuilder.BuildNotification();
        AppNotificationManager.Default.Show(notification);
    }

    private ExceptionHandlingOptions GetReportActionsForContext(Context context)
    {
        return context switch
        {
            Context.MainThreadException => _options!.MainThreadExceptionHandling,
            Context.BackgroundThreadException => _options!.AppDomainUnhandledExceptionHandling,
            Context.UnobservedTaskException => _options!.UnobservedTaskExceptionHandling,
            Context.AppDomainUnhandledException => _options!.AppDomainUnhandledExceptionHandling,
            _ => ExceptionHandlingOptions.None,
        };
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

    public void Dispose()
    {
        App.Current.UnhandledException -= App_UnhandledException;
        TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
    }

    /// <summary>
    /// Configuration options controlling how <see cref="GlobalErrorHandler"/> reacts to exceptions
    /// (what to log, what to show to the user, and where to store reports).
    /// </summary>
    internal sealed record Options
    {
        /// <summary>
        /// Gets the default configuration.
        /// </summary>
        public static Options Default { get; } = new();

        /// <summary>
        /// Gets the handling options applied to exceptions thrown on the main UI thread.
        /// </summary>
        public ExceptionHandlingOptions MainThreadExceptionHandling { get; init; } = ExceptionHandlingOptions.WriteToLog | ExceptionHandlingOptions.ShowDialog | ExceptionHandlingOptions.CreateReport;

        /// <summary>
        /// Gets the handling options applied to unobserved task exceptions.
        /// </summary>
        public ExceptionHandlingOptions UnobservedTaskExceptionHandling { get; init; } = ExceptionHandlingOptions.WriteToLog | ExceptionHandlingOptions.ShowNotification;

        /// <summary>
        /// Gets the handling options applied to unhandled exceptions raised by the application domain (typically background threads).
        /// </summary>
        public ExceptionHandlingOptions AppDomainUnhandledExceptionHandling { get; init; } = ExceptionHandlingOptions.WriteToLog | ExceptionHandlingOptions.ShowNotification | ExceptionHandlingOptions.CreateReport;

        /// <summary>
        /// Gets a predicate determining whether a given exception should be handled by the <see cref="GlobalErrorHandler"/>.
        /// Returning <see langword="false"/> skips any handling for the exception.
        /// </summary>
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

        /// <summary>
        /// Gets a value indicating whether Personally Identifiable Information (PII) should be redacted in error reports.
        /// </summary>
        public bool RedactPii { get; init; } = true;

        /// <summary>
        /// Gets a value indicating whether to store the error report on the user's desktop in addition to the log directory.
        /// </summary>
        public bool StoreReportOnUserDesktop { get; init; }
    }

    /// <summary>
    /// Flags that control how exceptions are handled and reported by <see cref="GlobalErrorHandler"/>.
    /// Multiple options can be combined.
    /// </summary>
    [Flags]
    internal enum ExceptionHandlingOptions
    {
        /// <summary>
        /// Do not perform any handling actions.
        /// </summary>
        None = 0x0,

        /// <summary>
        /// Write the exception details to the application log.
        /// </summary>
        WriteToLog = 0x1,

        /// <summary>
        /// Show a system notification summarizing the error and optionally linking to the saved report.
        /// </summary>
        ShowNotification = 0x2,

        /// <summary>
        /// Show the in-app error dialog with details and troubleshooting options.
        /// </summary>
        ShowDialog = 0x4,

        /// <summary>
        /// Create a textual error report on disk.
        /// </summary>
        CreateReport = 0x8,
    }

    private enum Context
    {
        Unknown = 0,
        MainThreadException,
        BackgroundThreadException,
        UnobservedTaskException,
        AppDomainUnhandledException,
    }
}
