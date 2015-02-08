using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Wox.Core.Exception;
using Wox.Infrastructure.Logger;
using MessageBox = System.Windows.MessageBox;

namespace Wox.Helper
{
    public static class ErrorReporting
    {
        public static void Report(Exception e)
        {
            Log.Error(ExceptionFormatter.FormatExcpetion(e));
            new CrashReporter.CrashReporter(e).Show();
        }

        public static void UnhandledExceptionHandle(object sender, UnhandledExceptionEventArgs e)
        {
            //handle non-ui thread exceptions
            App.Window.Dispatcher.Invoke(new Action(() =>
            {
                Report((Exception)e.ExceptionObject);
                if (!(e.ExceptionObject is WoxException))
                {
                    Environment.Exit(0);
                }
            }));
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
