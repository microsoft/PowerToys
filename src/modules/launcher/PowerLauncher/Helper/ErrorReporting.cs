// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
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
            else if (IsExceptionFromUserPlugin(e))
            {
                // Exceptions thrown by user-installed third-party plugins should be logged
                // but must not show the crash report window, since the exception is not
                // caused by PowerToys itself and the popup is confusing and alarming to the user.
                var logger = LogManager.GetLogger(LoggerName);
                logger.Error($"From {(isNotUIThread ? "non" : string.Empty)} UI thread - Exception from user plugin: {ExceptionFormatter.FormatException(e)}");
            }
            else
            {
                Report(e, isNotUIThread);
            }
        }

        /// <summary>
        /// Determines whether an exception originated from a user-installed third-party plugin
        /// (i.e., an assembly loaded from <see cref="Constant.PluginsDirectory"/>).
        /// </summary>
        private static bool IsExceptionFromUserPlugin(Exception e)
        {
            if (e == null)
            {
                return false;
            }

            try
            {
                var pluginsDir = Constant.PluginsDirectory;
                var stackTrace = new StackTrace(e, fNeedFileInfo: false);

                foreach (var frame in stackTrace.GetFrames() ?? [])
                {
                    var assemblyLocation = frame.GetMethod()?.DeclaringType?.Assembly?.Location;
                    if (!string.IsNullOrEmpty(assemblyLocation) &&
                        assemblyLocation.StartsWith(pluginsDir, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (System.Exception)
            {
                // If we cannot inspect the stack trace, assume the exception is not from a user plugin.
            }

            return false;
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
