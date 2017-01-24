using System;
using System.Windows;
using System.Windows.Threading;
using NLog;
using Wox.Infrastructure.Exception;

namespace Wox.Helper
{
    public static class ErrorReporting
    {
        public static void Report(Exception e)
        {
            var logger = LogManager.GetLogger("UnHandledException");
            logger.Fatal(ExceptionFormatter.FormatExcpetion(e));
            new CrashReporter(e).Show();
        }

        public static void UnhandledExceptionHandle(object sender, UnhandledExceptionEventArgs e)
        {
            //handle non-ui thread exceptions
            Application.Current.MainWindow.Dispatcher.Invoke(() =>
            {
                Report((Exception)e.ExceptionObject);
                if (!(e.ExceptionObject is WoxException))
                {
                    Environment.Exit(0);
                }
            });
        }

        public static void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            //handle ui thread exceptions
            Report(e.Exception);
            //prevent crash
            e.Handled = true;
        }
    }
}
