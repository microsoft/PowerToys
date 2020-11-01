// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using FancyZonesEditor.Models;
using ManagedCommon;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Non-localizable strings
        private const string CrashReportLogFile = "FZEditorCrashLog.txt";
        private const string PowerToysIssuesURL = "https://aka.ms/powerToysReportBug";

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

        public Settings ZoneSettings { get; }

        public App()
        {
            ZoneSettings = new Settings();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            RunnerHelper.WaitForPowerToysRunner(Settings.PowerToysPID, () =>
            {
                Environment.Exit(0);
            });

            LayoutModel foundModel = null;

            foreach (LayoutModel model in ZoneSettings.DefaultModels)
            {
                if (model.Type == Settings.ActiveZoneSetLayoutType)
                {
                    // found match
                    foundModel = model;
                    break;
                }
            }

            if (foundModel == null)
            {
                foreach (LayoutModel model in Settings.CustomModels)
                {
                    if ("{" + model.Guid.ToString().ToUpper() + "}" == Settings.ActiveZoneSetUUid.ToUpper())
                    {
                        // found match
                        foundModel = model;
                        break;
                    }
                }
            }

            if (foundModel == null)
            {
                foundModel = ZoneSettings.DefaultModels[0];
            }

            foundModel.IsSelected = true;

            EditorOverlay overlay = new EditorOverlay();
            overlay.Show();
            overlay.DataContext = foundModel;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            var fileStream = File.OpenWrite(CrashReportLogFile);
            var sw = new StreamWriter(fileStream);
            sw.Write(FormatException((Exception)args.ExceptionObject));
            fileStream.Close();
            MessageBox.Show(
                FancyZonesEditor.Properties.Resources.Crash_Report_Message_Box_Text_Part1 +
                Path.GetFullPath(fileStream.Name) +
                "\n" +
                FancyZonesEditor.Properties.Resources.Crash_Report_Message_Box_Text_Part2 +
                PowerToysIssuesURL,
                FancyZonesEditor.Properties.Resources.Fancy_Zones_Editor_App_Title);
        }

        private string FormatException(Exception ex)
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
    }
}
