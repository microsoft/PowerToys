// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.PowerToys.Settings.UI.BugReport;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.Win32;
using WinUIEx;
using static Microsoft.PowerToys.Settings.UI.ViewModels.GeneralViewModel;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class BugReportViewModel : Observable
    {
        private static readonly Lock LockObj = new Lock();

        private BugReportWindow _mainWindow;
        private const string InstallScopeRegKey = @"Software\Classes\powertoys\";

        private static volatile bool isBugReportThreadRunning;

        public static Uri NewIssueLink { get; set; }

        private bool _isBugReportMessageVisible;

        public bool IsBugReportMessageVisible
        {
            get
            {
                return _isBugReportMessageVisible;
            }

            set
            {
                _isBugReportMessageVisible = value;
                OnPropertyChanged(nameof(IsBugReportMessageVisible));
            }
        }

        private string _bugReportMessage;

        public string BugReportMessage
        {
            get
            {
                return _bugReportMessage;
            }

            set
            {
                _bugReportMessage = value;
                OnPropertyChanged(nameof(BugReportMessage));
            }
        }

        public BugReportViewModel(BugReportWindow mainWindow)
        {
            _mainWindow = mainWindow;

            InitializeReportBugLink();
        }

        internal void Close()
        {
            _mainWindow.Hide();
        }

        public void InitializeReportBugLink()
        {
            var version = GetPowerToysVersion();

            string isElevatedString = "PowerToys is running " + (IsAdministrator() ? "as admin (elevated)" : "as user (non-elevated)");

            string installScope = GetCurrentInstallScope() == InstallScope.PerMachine ? "per machine (system)" : "per user";

            var info = $"OS Version: {GetOSVersion()} \n.NET Version: {GetDotNetVersion()}\n{isElevatedString}\nInstall scope: {installScope}\nOperating System Language: {CultureInfo.InstalledUICulture.DisplayName}\nSystem locale: {CultureInfo.InstalledUICulture.Name}";

            var gitHubURL = "https://github.com/microsoft/PowerToys/issues/new?template=bug_report.yml&labels=Issue-Bug%2CTriage-Needed" +
                "&version=" + version + "&additionalInfo=" + System.Web.HttpUtility.UrlEncode(info);

            NewIssueLink = new Uri(gitHubURL);
        }

        private string GetPowerToysVersion()
        {
            return Helper.GetProductVersion().TrimStart('v');
        }

        private string GetOSVersion()
        {
            return Environment.OSVersion.VersionString;
        }

        public static string GetDotNetVersion()
        {
            return $".NET {Environment.Version}";
        }

        public static InstallScope GetCurrentInstallScope()
        {
            // Check HKLM first
            if (Registry.LocalMachine.OpenSubKey(InstallScopeRegKey) != null)
            {
                return InstallScope.PerMachine;
            }

            // If not found, check HKCU
            var userKey = Registry.CurrentUser.OpenSubKey(InstallScopeRegKey);
            if (userKey != null)
            {
                var installScope = userKey.GetValue("InstallScope") as string;
                userKey.Close();
                if (!string.IsNullOrEmpty(installScope) && installScope.Contains("perUser"))
                {
                    return InstallScope.PerUser;
                }
            }

            return InstallScope.PerMachine; // Default if no specific registry key found
        }

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        internal void LaunchBugReportTool()
        {
            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;
            var directoryPath = System.AppContext.BaseDirectory;
            var basePath = Directory.GetParent(directoryPath.TrimEnd('\\')).FullName;
            var bugReportPath = Path.Combine(basePath, "Tools", "PowerToys.BugReportTool.exe");
            IsBugReportMessageVisible = true;

            lock (LockObj)
            {
                if (!isBugReportThreadRunning)
                {
                    isBugReportThreadRunning = true;
                    BugReportMessage = resourceLoader.GetString("BugReportUnderConstruction");

                    Thread thread = new Thread(() =>
                    {
                        try
                        {
                            ProcessStartInfo psi = new ProcessStartInfo
                            {
                                FileName = bugReportPath,
                                UseShellExecute = true,
                                WindowStyle = ProcessWindowStyle.Hidden,
                            };

                            using (Process process = Process.Start(psi))
                            {
                                if (process != null)
                                {
                                    process.WaitForExit();
                                }

                                BugReportMessage = resourceLoader.GetString("BugReportReady");
                            }
                        }
                        finally
                        {
                            lock (LockObj)
                            {
                                isBugReportThreadRunning = false;
                            }
                        }
                    });

                    thread.IsBackground = true;
                    thread.Start();
                }
            }
        }
    }
}
