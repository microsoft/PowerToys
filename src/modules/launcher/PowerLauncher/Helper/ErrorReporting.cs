// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Threading;
using NLog;
using Wox.Infrastructure.Exception;
using Wox.Plugin;

namespace PowerLauncher.Helper
{
    public static class ErrorReporting
    {
        private static void Report(Exception e, bool waitForClose)
        {
            if (e != null)
            {
                var logger = LogManager.GetLogger("UnHandledException");
                logger.Fatal(ExceptionFormatter.FormatException(e));

                var reportWindow = new ReportWindow(e);

                if (waitForClose)
                {
                    reportWindow.ShowDialog();
                }
                else
                {
                    reportWindow.Show();
                }
            }
        }

        public static void ShowMessageBox(string title, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title);
            });
        }

        public static void UnhandledExceptionHandle(object sender, UnhandledExceptionEventArgs e)
        {
            // handle non-ui thread exceptions
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Report((Exception)e?.ExceptionObject, true);
            });
        }

        public static void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // handle ui thread exceptions
            Report(e?.Exception, false);

            // prevent application exist, so the user can copy prompted error info
            e.Handled = true;
        }

        public static string RuntimeInfo()
        {
            var info = $"\nVersion: {Constant.Version}" +
                       $"\nOS Version: {Environment.OSVersion.VersionString}" +
                       $"\nIntPtr Length: {IntPtr.Size}" +
                       $"\nx64: {Environment.Is64BitOperatingSystem}";
            return info;
        }
    }
}
