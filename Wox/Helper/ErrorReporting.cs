using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Wox.Core.Exception;
using Wox.Infrastructure.Logger;

namespace Wox.Helper
{
    public static class ErrorReporting
    {
        private static void Report(Exception e)
        {
            //if (Debugger.IsAttached) return;
            Log.Error(ExceptionFormatter.FormatExcpetion(e));
            new CrashReporter.CrashReporter(e).Show();
        }

        public static void UnhandledExceptionHandle(object sender, UnhandledExceptionEventArgs e)
        {
            Report((Exception)e.ExceptionObject);
        }

        public static void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Report(e.Exception);
        }

        public static void ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Report(e.Exception);
        }
    }
}
