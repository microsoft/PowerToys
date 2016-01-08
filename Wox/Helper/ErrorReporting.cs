using System;
using System.Windows.Threading;
using Wox.Infrastructure.Exception;
using Wox.Infrastructure.Logger;

namespace Wox.Helper
{
    public static class ErrorReporting
    {
        public static void Report(Exception e)
        {
            Log.Fatal(e);
            new CrashReporter.CrashReporter(e).Show();
        }

        public static void UnhandledExceptionHandle(object sender, UnhandledExceptionEventArgs e)
        {
            //handle non-ui thread exceptions
            App.Window.Dispatcher.Invoke(() =>
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
