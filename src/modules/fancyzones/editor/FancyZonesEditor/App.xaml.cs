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
            var fileStream = File.OpenWrite("FZEditorCrashLog.txt");
            var sw = new StreamWriter(fileStream);
            sw.Write(FormatException((Exception)args.ExceptionObject));
            fileStream.Close();
            MessageBox.Show("FancyZones editor crash log written to " + Path.GetFullPath(fileStream.Name));
        }

        private string FormatException(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("## Exception");
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

            sb.AppendLine("## Environment");
            sb.AppendLine($"* Command Line: {Environment.CommandLine}");
            sb.AppendLine($"* Timestamp: {DateTime.Now.ToString(CultureInfo.InvariantCulture)}");
            sb.AppendLine($"* OS Version: {Environment.OSVersion.VersionString}");
            sb.AppendLine($"* IntPtr Length: {IntPtr.Size}");
            sb.AppendLine($"* x64: {Environment.Is64BitOperatingSystem}");
            sb.AppendLine($"* CLR Version: {Environment.Version}");
            sb.AppendLine("## Assemblies - " + AppDomain.CurrentDomain.FriendlyName);
            sb.AppendLine();
            foreach (var ass in AppDomain.CurrentDomain.GetAssemblies().OrderBy(o => o.GlobalAssemblyCache ? 50 : 0))
            {
                sb.Append("* ");
                sb.Append(ass.FullName);
                sb.Append(" (");

                if (ass.IsDynamic)
                {
                    sb.Append("dynamic assembly doesn't has location");
                }
                else if (string.IsNullOrEmpty(ass.Location))
                {
                    sb.Append("location is null or empty");
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
