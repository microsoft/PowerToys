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
        private const string LoggerName = "UnHandledException";

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
                HandleException(e?.ExceptionObject as Exception, true);
            });
        }

        public static void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (e != null)
            {
                // handle ui thread exceptions
                HandleException(e.Exception, false);

                // prevent application exist, so the user can copy prompted error info
                e.Handled = true;
            }
        }

        public static string RuntimeInfo()
        {
            var info = $"\nVersion: {Constant.Version}" +
                       $"\nOS Version: {Environment.OSVersion.VersionString}" +
                       $"\nIntPtr Length: {IntPtr.Size}" +
                       $"\nx64: {Environment.Is64BitOperatingSystem}";
            return info;
        }

        private static void HandleException(Exception e, bool isNotUIThread)
        {
            // The crash occurs in PresentationFramework.dll, not necessarily when the Runner UI is visible, originating from this line:
            // https://github.com/dotnet/wpf/blob/3439f20fb8c685af6d9247e8fd2978cac42e74ac/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Shell/WindowChromeWorker.cs#L1005
            // Many bug reports because users see the "Report problem UI" after "the" crash with System.Runtime.InteropServices.COMException 0xD0000701 or 0x80263001.
            // However, displaying this "Report problem UI" during WPF crashes, especially when DWM composition is changing, is not ideal; some users reported it hangs for up to a minute before the "Report problem UI" appears.
            // This change modifies the behavior to log the exception instead of showing the "Report problem UI".
            if (ExceptionHelper.IsRecoverableDwmCompositionException(e as System.Runtime.InteropServices.COMException))
            {
                var logger = LogManager.GetLogger(LoggerName);
                logger.Error($"From {(isNotUIThread ? "non" : string.Empty)} UI thread's exception: {ExceptionFormatter.FormatException(e)}");
            }
            else
            {
                Report(e, isNotUIThread);
            }
        }

        private static void Report(Exception e, bool waitForClose)
        {
            if (e != null)
            {
                var logger = LogManager.GetLogger(LoggerName);
                logger.Fatal($"From {(waitForClose ? "non" : string.Empty)} UI thread's exception: {ExceptionFormatter.FormatException(e)}");

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
    }
}
