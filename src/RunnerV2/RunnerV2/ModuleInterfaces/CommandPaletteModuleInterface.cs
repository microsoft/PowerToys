// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;
using RunnerV2.Helpers;
using Windows.ApplicationModel;

namespace RunnerV2.ModuleInterfaces
{
    internal sealed class CommandPaletteModuleInterface : IPowerToysModule
    {
        private const string PackageName = "Microsoft.CommandPalette"
#if DEBUG
            + ".Dev"
#endif
            ;

        public string Name => "CmdPal";

        public bool Enabled => new SettingsUtils().GetSettingsOrDefault<GeneralSettings>().Enabled.CmdPal;

        public GpoRuleConfigured GpoRuleConfigured => GPOWrapper.GetConfiguredCmdPalEnabledValue();

        public void Disable()
        {
            ProcessHelper.ScheudleProcessKill("Microsoft.CmdPal.UI");
            lock (_launchedLock)
            {
                _launched = false;
            }
        }

        public void Enable()
        {
            if (PackageHelper.GetRegisteredPackage(PackageName, false) is null)
            {
                try
                {
                    string architectureString = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "x64" : "ARM64";
#if DEBUG
                    string[] msixFiles = PackageHelper.FindMsixFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\WinUI3Apps\\CmdPal\\AppPackages\\Microsoft.CmdPal.UI_0.0.1.0_Debug_Test\\", false);
                    string[] dependencies = PackageHelper.FindMsixFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\WinUI3Apps\\CmdPal\\AppPackages\\Microsoft.CmdPal.UI_0.0.1.0_Debug_Test\\Dependencies\\" + architectureString + "\\", true);
#else
                    string[] msixFiles = PackageHelper.FindMsixFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\WinUI3Apps\\CmdPal\\", false);
                    string[] dependencies = PackageHelper.FindMsixFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\WinUI3Apps\\CmdPal\\Dependencies\\", true);
#endif

                    if (msixFiles.Length > 0)
                    {
                        if (!PackageHelper.RegisterPackage(msixFiles[0], dependencies))
                        {
                            Logger.LogError("Failed to register Command Palette package.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Exception occurred while enabling Command Palette package.", ex);
                }
            }

            if (PackageHelper.GetRegisteredPackage(PackageName, false) is null)
            {
                Logger.LogError("Command Palette package is not registered after attempting to enable it.");
                return;
            }

            lock (_launchedLock)
            {
                if (_launched)
                {
                    return;
                }
            }

            LaunchApp("explorer.exe", "x-cmdpal://background", false);
        }

        private readonly object _launchedLock = new();
        private bool _launched;

        // TODO: Implement retry logic for launching the app
        /*private static void TryLaunch(string path, string args)
        {
            int baseDelay = 1000;
            int maxAttempts = 9;
            int retryCount = 0;

            do
            {
                if (LaunchApp)
            }
        }*/

        private bool LaunchApp(string path, string args, bool elevated)
        {
            try
            {
                Process? process = Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    Verb = elevated ? "runas" : "open",
                    Arguments = args,
                });

                if (process is null)
                {
                    Logger.LogError($"Failed to start process for {path} with args {args}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception occurred while launching app {path} with args {args}", ex);
                return false;
            }

            lock (_launchedLock)
            {
                _launched = true;
            }

            return true;
        }
    }
}
