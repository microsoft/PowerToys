// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels.Commands;
using Windows.Management.Deployment;

using Package = global::Windows.ApplicationModel.Package;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class CmdPalViewModel : Observable
    {
        private static readonly string PackageName = "Microsoft.CmdPal.POC";

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _isEnabled;

        public ButtonClickCommand InstallModuleEventHandler => new(InstallModule);

        public ButtonClickCommand UninstallModuleEventHandler => new(UninstallModule);

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().Location;
                UriBuilder uri = new(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        public CmdPalViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            ArgumentNullException.ThrowIfNull(settingsUtils);

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredCmdPalEnabledValue();
            if (_enabledGpoRuleConfiguration is GpoRuleConfigured.Disabled or GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                IsEnabledGpoConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.CmdPal;
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;

            set
            {
                if (IsEnabledGpoConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _isEnabled)
                {
                    _isEnabled = value;

                    // Set the status in the general settings configuration
                    GeneralSettingsConfig.Enabled.AlwaysOnTop = value;
                    OutGoingGeneralSettings snd = new(GeneralSettingsConfig);

                    SendConfigMSG(snd.ToString());
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public bool IsEnabledGpoConfigured { get; private set; }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        public bool IsCmdPalInstalled => GetInstalledCmdPalPackage(PackageName) is not null;

        private Package GetInstalledCmdPalPackage(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                Logger.LogError("Package name cannot be null or empty.");
            }

            try
            {
                PackageManager packageManager = new();
                System.Collections.Generic.IEnumerable<Package> packages = packageManager.FindPackagesForUser(string.Empty);

                foreach (Package package in packages)
                {
                    if (package.Id.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase))
                    {
                        return package;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error while checking if package is installed: {ex.Message}");
                return null;
            }
        }

        public static string FindMsixFile(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                Logger.LogError("Directory path cannot be null or empty.");
            }

            if (!Directory.Exists(directoryPath))
            {
                Logger.LogError($"The directory '{directoryPath}' does not exist.");
            }

            string pattern = @"^.+\.(msix|msixbundle)$";
            Regex regex = new(pattern, RegexOptions.IgnoreCase);

            string matchedFile = string.Empty;
            try
            {
                matchedFile = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                                            .Where(file => regex.IsMatch(Path.GetFileName(file)))
                                            .ToArray().FirstOrDefault();
            }
            catch (Exception ex)
            {
                Logger.LogError($"An error occurred while searching for MSIX files: {ex.Message}");
            }

            return matchedFile;
        }

        private void InstallModule()
        {
            string msixFilePath = FindMsixFile(AssemblyDirectory + "\\CmdPal\\");

            if (string.IsNullOrWhiteSpace(msixFilePath))
            {
                Logger.LogError($"MSIX file path cannot be null or empty: {msixFilePath}");
                return;
            }

            if (!File.Exists(msixFilePath))
            {
                Logger.LogError($"The specified MSIX file was not found: {msixFilePath}");
                return;
            }

            try
            {
                PackageManager packageManager = new();
                System.Threading.Tasks.Task<DeploymentResult> deploymentResult = packageManager.AddPackageAsync(
                    new Uri(msixFilePath),
                    null,
                    DeploymentOptions.None).AsTask();

                deploymentResult.Wait();

                if (deploymentResult.Result.ExtendedErrorCode != null)
                {
                    Logger.LogError($"Package installation failed with error: {deploymentResult.Result.ExtendedErrorCode.Message}");
                }
                else
                {
                    Logger.LogInfo("Package installed successfully!");

                    OnPropertyChanged(nameof(IsCmdPalInstalled));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"An error occurred while installing the package: {ex.Message}");
            }
        }

        private void UninstallModule()
        {
            try
            {
                Package package = GetInstalledCmdPalPackage(PackageName);

                if (package is null)
                {
                    return; // Package is not installed, shouldn't happen ever
                }

                PackageManager packageManager = new();

                System.Threading.Tasks.Task<DeploymentResult> uninstallOperation = packageManager.RemovePackageAsync(package.Id.FullName).AsTask();
                uninstallOperation.Wait();

                if (uninstallOperation.Result.ExtendedErrorCode != null && uninstallOperation.Result.ExtendedErrorCode.HResult != 0)
                {
                    Logger.LogError($"Failed to uninstall package: {uninstallOperation.Result.ErrorText}");
                }
                else
                {
                    Logger.LogInfo($"Package '{package.Id.FullName}' was uninstalled successfully.");

                    OnPropertyChanged(nameof(IsCmdPalInstalled));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"An error occurred while uninstalling the package: {ex.Message}");
            }
        }
    }
}
