using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Xml;
using Microsoft.Win32;
using Wox.Infrastructure.Logger;
using CrashReporterDotNET;

namespace Wox.Helper.ErrorReporting
{
    public static class ErrorReporting
    {
        private static void ReportCrash(Exception exception)
        {
            var reportCrash = new ReportCrash
            {
                ToEmail = "qianlf2008@163.com"
            };

            reportCrash.Send(exception);
        }

        public static void UnhandledExceptionHandle(object sender, System.UnhandledExceptionEventArgs e)
        {
            if (System.Diagnostics.Debugger.IsAttached) return;

            ReportCrash((Exception)e.ExceptionObject);
            return;
            string error = CreateExceptionReport("System.AppDomain.UnhandledException", e.ExceptionObject);

            //e.IsTerminating is always true in most times, so try to avoid use this property
            //http://stackoverflow.com/questions/10982443/what-causes-the-unhandledexceptioneventargs-isterminating-flag-to-be-true-or-fal
            Log.Error(error);
            TryShowErrorMessageBox(error, e.ExceptionObject);
        }

        public static void DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached) return;

            ReportCrash(e.Exception);
            return;

            e.Handled = true;
            string error = CreateExceptionReport("System.Windows.Application.DispatcherUnhandledException", e.Exception);

            Log.Error(error);
            TryShowErrorMessageBox(error, e.Exception);
        }
        public static void ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            if (Debugger.IsAttached) return;
            ReportCrash(e.Exception);
            return;
            string error = CreateExceptionReport("System.Windows.Forms.Application.ThreadException", e.Exception);

            Log.Fatal(error);
            TryShowErrorMessageBox(error, e.Exception);
        }

        private static string CreateExceptionReport(string ev, object exceptionObject)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Exception");
            sb.AppendLine();
            sb.AppendLine("```");

            var ex = exceptionObject as Exception;
            if (ex != null)
            {
                var exlist = new List<StringBuilder>();

                while (ex != null)
                {
                    var exsb = new StringBuilder();
                    exsb.Append(ex.GetType().FullName);
                    exsb.Append(": ");
                    exsb.AppendLine(ex.Message);
                    if (ex.Source != null)
                    {
                        exsb.Append("   Source: ");
                        exsb.AppendLine(ex.Source);
                    }
                    if (ex.TargetSite != null)
                    {
                        exsb.Append("   TargetAssembly: ");
                        exsb.AppendLine(ex.TargetSite.Module.Assembly.ToString());
                        exsb.Append("   TargetModule: ");
                        exsb.AppendLine(ex.TargetSite.Module.ToString());
                        exsb.Append("   TargetSite: ");
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
            }
            else
            {
                sb.AppendLine(exceptionObject.GetType().FullName);
                sb.AppendLine(new StackTrace().ToString());
                sb.AppendLine("```");
                sb.AppendLine();
            }

            sb.AppendLine("## Environment");
            sb.AppendLine();
            sb.Append("* Command Line: ");
            sb.AppendLine(Environment.CommandLine);
            sb.Append("* Exception Handle: ");
            sb.AppendLine(ev);
            sb.Append("* Timestamp: ");
            sb.AppendLine(XmlConvert.ToString(DateTime.Now));
            sb.Append("* IntPtr Length: ");
            sb.AppendLine(IntPtr.Size.ToString());
            sb.Append("* System Version: ");
            sb.AppendLine(Environment.OSVersion.VersionString);
            sb.Append("* CLR Version: ");
            sb.AppendLine(Environment.Version.ToString());
            sb.AppendLine("* Installed .NET Framework: ");
            foreach (var result in GetFrameworkVersionFromRegistry())
            {
                sb.Append("   * ");
                sb.AppendLine(result);
            }

            sb.AppendLine();
            sb.AppendLine("## Assemblies - " + System.AppDomain.CurrentDomain.FriendlyName);
            sb.AppendLine();
            foreach (var ass in System.AppDomain.CurrentDomain.GetAssemblies().OrderBy(o => o.GlobalAssemblyCache ? 100 : 0))
            {
                sb.Append("* ");
                sb.Append(ass.FullName);
                sb.Append(" (");
                sb.Append(SyntaxSugars.CallOrRescueDefault(() => ass.Location, "not supported"));
                sb.AppendLine(")");
            }

            var process = System.Diagnostics.Process.GetCurrentProcess();
            sb.AppendLine();
            sb.AppendLine("## Modules - " + process.ProcessName);
            sb.AppendLine();
            foreach (ProcessModule mod in process.Modules)
            {
                sb.Append("* ");
                sb.Append(mod.FileName);
                sb.Append(" (");
                sb.Append(mod.FileVersionInfo.FileDescription);
                sb.Append(", ");
                sb.Append(mod.FileVersionInfo.FileVersion);
                sb.Append(", ");
                sb.Append(mod.FileVersionInfo.ProductName);
                sb.Append(", ");
                sb.Append(mod.FileVersionInfo.ProductVersion);
                sb.Append(", ");
                sb.Append(mod.FileVersionInfo.CompanyName);
                sb.Append("), ");
                sb.Append(string.Format("0x{0:X16}", mod.BaseAddress.ToInt64()));
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("## Threads - " + process.Threads.Count);
            sb.AppendLine();
            foreach (ProcessThread th in process.Threads)
            {
                sb.Append("* ");
                sb.AppendLine(string.Format("{0}, {1} {2}, Started: {3}, StartAddress: 0x{4:X16}", th.Id, th.ThreadState, th.PriorityLevel, th.StartTime, th.StartAddress.ToInt64()));
            }

            return sb.ToString();
        }

        // http://msdn.microsoft.com/en-us/library/hh925568%28v=vs.110%29.aspx
        private static List<string> GetFrameworkVersionFromRegistry()
        {
            try
            {
                var result = new List<string>();
                using (RegistryKey ndpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\"))
                {
                    foreach (string versionKeyName in ndpKey.GetSubKeyNames())
                    {
                        if (versionKeyName.StartsWith("v"))
                        {
                            RegistryKey versionKey = ndpKey.OpenSubKey(versionKeyName);
                            string name = (string)versionKey.GetValue("Version", "");
                            string sp = versionKey.GetValue("SP", "").ToString();
                            string install = versionKey.GetValue("Install", "").ToString();
                            if (install != "")
                                if (sp != "" && install == "1")
                                    result.Add(string.Format("{0} {1} SP{2}", versionKeyName, name, sp));
                                else
                                    result.Add(string.Format("{0} {1}", versionKeyName, name));

                            if (name != "")
                            {
                                continue;
                            }
                            foreach (string subKeyName in versionKey.GetSubKeyNames())
                            {
                                RegistryKey subKey = versionKey.OpenSubKey(subKeyName);
                                name = (string)subKey.GetValue("Version", "");
                                if (name != "")
                                    sp = subKey.GetValue("SP", "").ToString();
                                install = subKey.GetValue("Install", "").ToString();
                                if (install != "")
                                {
                                    if (sp != "" && install == "1")
                                        result.Add(string.Format("{0} {1} {2} SP{3}", versionKeyName, subKeyName, name, sp));
                                    else if (install == "1")
                                        result.Add(string.Format("{0} {1} {2}", versionKeyName, subKeyName, name));
                                }

                            }

                        }
                    }
                }
                using (RegistryKey ndpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
                {
                    int releaseKey = (int)ndpKey.GetValue("Release");
                    {
                        if (releaseKey == 378389)
                            result.Add("v4.5");

                        if (releaseKey == 378675)
                            result.Add("v4.5.1 installed with Windows 8.1");

                        if (releaseKey == 378758)
                            result.Add("4.5.1 installed on Windows 8, Windows 7 SP1, or Windows Vista SP2");
                    }
                }
                return result;
            }
            catch (Exception e)
            {
                return new List<string>();
            }

        }

        public static bool TryShowErrorMessageBox(string error, object exceptionObject)
        {
            var title = "Wox - Unhandled Exception";

            try
            {
                ShowWPFDialog(error, title, exceptionObject);
                return true;
            }
            catch { }

            error = "Wox has occured an error that can't be handled. " + Environment.NewLine + Environment.NewLine + error;

            try
            {
                ShowWPFMessageBox(error, title);
                return true;
            }
            catch { }

            try
            {
                ShowWindowsFormsMessageBox(error, title);
                return true;
            }
            catch { }

            return true;
        }

        private static void ShowWPFDialog(string error, string title, object exceptionObject)
        {
            var dialog = new WPFErrorReportingDialog(error, title, exceptionObject);
            dialog.ShowDialog();
        }

        private static void ShowWPFMessageBox(string error, string title)
        {
            System.Windows.MessageBox.Show(error, title, MessageBoxButton.OK, MessageBoxImage.Error,
                        MessageBoxResult.OK, System.Windows.MessageBoxOptions.None);
        }
        private static void ShowWindowsFormsMessageBox(string error, string title)
        {
            System.Windows.Forms.MessageBox.Show(error, title, MessageBoxButtons.OK,
                        MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
        }
    }
}
