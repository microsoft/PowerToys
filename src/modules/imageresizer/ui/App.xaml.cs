// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Utilities;
using ImageResizer.ViewModels;
using ImageResizer.Views;
using Microsoft.PowerToys.Common.UI;

namespace ImageResizer
{
    public partial class App : Application, IDisposable
    {
        // Non-localizable strings
        private const string CrashReportExceptionTag = "Exception";
        private const string CrashReportSourceTag = "Source: ";
        private const string CrashReportTargetAssemblyTag = "TargetAssembly: ";
        private const string CrashReportTargetModuleTag = "TargetModule: ";
        private const string CrashReportTargetSiteTag = "TargetSite: ";
        private const string CrashReportEnvironmentTag = "Environment";
        private const string CrashReportCommandLineTag = "* Command Line: ";
        private const string CrashReportTimestampTag = "* Timestamp: ";
        private const string CrashReportOSVersionTag = "* OS Version: ";
        private const string CrashReportIntPtrLengthTag = "* IntPtr Length: ";
        private const string CrashReportx64Tag = "* x64: ";
        private const string CrashReportCLRVersionTag = "* CLR Version: ";
        private const string CrashReportAssembliesTag = "Assemblies - ";
        private const string CrashReportDynamicAssemblyTag = "dynamic assembly doesn't have location";
        private const string CrashReportLocationNullTag = "location is null or empty";
        private const string PowerToysIssuesURL = "https://aka.ms/powerToysReportBug";

        private ThemeManager _themeManager;
        private bool _isDisposed;

        static App()
        {
            Console.InputEncoding = Encoding.Unicode;
        }

        private const string CrashReportLogFile = "ImageResizerCrashReport.txt";

        private static void ShowReportMessageBox(string fileName)
        {
            MessageBox.Show(
                ImageResizer.Properties.Resources.Crash_Report_Message_Box_Text_Part1 +
                Path.GetFullPath(fileName) +
                "\n" +
                ImageResizer.Properties.Resources.Crash_Report_Message_Box_Text_Part2 +
                PowerToysIssuesURL,
                "ImageResizer");
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            var fileStream = File.OpenWrite(CrashReportLogFile);
            using (var sw = new StreamWriter(fileStream))
            {
                sw.Write(FormatException((Exception)args.ExceptionObject));
            }

            fileStream.Close();

            ShowReportMessageBox(fileStream.Name);
        }

        private static string FormatException(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("## " + CrashReportExceptionTag);
            sb.AppendLine();
            sb.AppendLine("```");

            var exlist = new List<StringBuilder>();

            while (ex != null)
            {
                var exsb = new StringBuilder();
                exsb.Append(ex.GetType().FullName);
                exsb.Append(": ");
                exsb.AppendLine(ex.Message);
                if (ex.Source != null)
                {
                    exsb.Append("   " + CrashReportSourceTag);
                    exsb.AppendLine(ex.Source);
                }

                if (ex.TargetSite != null)
                {
                    exsb.Append("   " + CrashReportTargetAssemblyTag);
                    exsb.AppendLine(ex.TargetSite.Module.Assembly.ToString());
                    exsb.Append("   " + CrashReportTargetModuleTag);
                    exsb.AppendLine(ex.TargetSite.Module.ToString());
                    exsb.Append("   " + CrashReportTargetSiteTag);
                    exsb.AppendLine(ex.TargetSite.ToString());
                }

                exsb.AppendLine(ex.StackTrace);
                exlist.Add(exsb);

                ex = ex.InnerException;
            }

            foreach (var result in exlist.Select(o => o.ToString()).Reverse())
            {
                sb.AppendLine(result);
            }

            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("## " + CrashReportEnvironmentTag);
            sb.AppendLine(CrashReportCommandLineTag + Environment.CommandLine);

            // Using InvariantCulture since this is used for a timestamp internally
            sb.AppendLine(CrashReportTimestampTag + DateTime.Now.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(CrashReportOSVersionTag + Environment.OSVersion.VersionString);
            sb.AppendLine(CrashReportIntPtrLengthTag + IntPtr.Size);
            sb.AppendLine(CrashReportx64Tag + Environment.Is64BitOperatingSystem);
            sb.AppendLine(CrashReportCLRVersionTag + Environment.Version);
            sb.AppendLine("## " + CrashReportAssembliesTag + AppDomain.CurrentDomain.FriendlyName);
            sb.AppendLine();
            foreach (var ass in AppDomain.CurrentDomain.GetAssemblies().OrderBy(o => o.GlobalAssemblyCache ? 50 : 0))
            {
                sb.Append("* ");
                sb.Append(ass.FullName);
                sb.Append(" (");

                if (ass.IsDynamic)
                {
                    sb.Append(CrashReportDynamicAssemblyTag);
                }
                else if (string.IsNullOrEmpty(ass.Location))
                {
                    sb.Append(CrashReportLocationNullTag);
                }
                else
                {
                    sb.Append(ass.Location);
                }

                sb.AppendLine(")");
            }

            return sb.ToString();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            var batch = ResizeBatch.FromCommandLine(Console.In, e?.Args);

            // TODO: Add command-line parameters that can be used in lieu of the input page (issue #14)
            var mainWindow = new MainWindow(new MainViewModel(batch, Settings.Default));
            mainWindow.Show();

            _themeManager = new ThemeManager(this);

            // Temporary workaround for issue #1273
            BecomeForegroundWindow(new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle);
        }

        private static void BecomeForegroundWindow(IntPtr hWnd)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT { type = NativeMethods.INPUTTYPE.INPUT_MOUSE, data = { } };
            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[] { input };
            _ = NativeMethods.SendInput(1, inputs, NativeMethods.INPUT.Size);
            NativeMethods.SetForegroundWindow(hWnd);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _themeManager?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
