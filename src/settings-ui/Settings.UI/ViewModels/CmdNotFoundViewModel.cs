﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class CmdNotFoundViewModel : Observable
    {
        public ButtonClickCommand CheckRequirementsEventHandler => new ButtonClickCommand(CheckCommandNotFoundRequirements);

        public ButtonClickCommand InstallPowerShell7EventHandler => new ButtonClickCommand(InstallPowerShell7);

        public ButtonClickCommand InstallWinGetClientModuleEventHandler => new ButtonClickCommand(InstallWinGetClientModule);

        public ButtonClickCommand InstallModuleEventHandler => new ButtonClickCommand(InstallModule);

        public ButtonClickCommand UninstallModuleEventHandler => new ButtonClickCommand(UninstallModule);

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;

        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().Location;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        public CmdNotFoundViewModel()
        {
            InitializeEnabledValue();
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredCmdNotFoundEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
            }

            CheckCommandNotFoundRequirements();
        }

        private string _commandOutputLog;

        public string CommandOutputLog
        {
            get => _commandOutputLog;
            set
            {
                if (_commandOutputLog != value)
                {
                    _commandOutputLog = value;
                    OnPropertyChanged(nameof(CommandOutputLog));
                }
            }
        }

        private bool _isPowerShell7Detected;

        public bool IsPowerShell7Detected
        {
            get => _isPowerShell7Detected;
            set
            {
                if (_isPowerShell7Detected != value)
                {
                    _isPowerShell7Detected = value;
                    OnPropertyChanged(nameof(IsPowerShell7Detected));
                }
            }
        }

        private bool _isWinGetClientModuleDetected;

        public bool IsWinGetClientModuleDetected
        {
            get => _isWinGetClientModuleDetected;
            set
            {
                if (_isWinGetClientModuleDetected != value)
                {
                    _isWinGetClientModuleDetected = value;
                    OnPropertyChanged(nameof(IsWinGetClientModuleDetected));
                }
            }
        }

        private bool _isCommandNotFoundModuleInstalled;

        public bool IsCommandNotFoundModuleInstalled
        {
            get => _isCommandNotFoundModuleInstalled;
            set
            {
                if (_isCommandNotFoundModuleInstalled != value)
                {
                    _isCommandNotFoundModuleInstalled = value;
                    OnPropertyChanged(nameof(IsCommandNotFoundModuleInstalled));
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public string RunPowerShellScript(string powershellExecutable, string powershellArguments, bool hidePowerShellWindow = false)
        {
            string outputLog = string.Empty;
            try
            {
                var startInfo = new ProcessStartInfo()
                {
                    FileName = powershellExecutable,
                    Arguments = powershellArguments,
                    CreateNoWindow = hidePowerShellWindow,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                };
                startInfo.EnvironmentVariables["NO_COLOR"] = "1";
                var process = Process.Start(startInfo);
                while (!process.StandardOutput.EndOfStream)
                {
                    outputLog += process.StandardOutput.ReadLine() + "\r\n"; // Weirdly, PowerShell 7 won't give us new lines.
                }
            }
            catch (Exception ex)
            {
                outputLog = ex.ToString();
            }

            CommandOutputLog = outputLog;
            return outputLog;
        }

        public void CheckCommandNotFoundRequirements()
        {
            var ps1File = AssemblyDirectory + "\\Assets\\Settings\\Scripts\\CheckCmdNotFoundRequirements.ps1";
            var arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Unrestricted -File \"{ps1File}\"";
            var result = RunPowerShellScript("pwsh.exe", arguments, true);

            if (result.Contains("PowerShell 7.4 or greater detected."))
            {
                IsPowerShell7Detected = true;
            }
            else if (result.Contains("PowerShell 7.4 or greater not detected."))
            {
                IsPowerShell7Detected = false;
            }
            else if (result.Contains("pwsh.exe"))
            {
                // Likely an error saying there was an error starting pwsh.exe, so we can assume Powershell 7 was not detected.
                CommandOutputLog += "PowerShell 7.4 or greater not detected. Installation instructions can be found on https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows \r\n";
                IsPowerShell7Detected = false;
            }

            if (result.Contains("WinGet Client module detected."))
            {
                IsWinGetClientModuleDetected = true;
            }
            else if (result.Contains("WinGet Client module not detected."))
            {
                IsWinGetClientModuleDetected = false;
            }

            if (result.Contains("Command Not Found module is registered in the profile file."))
            {
                IsCommandNotFoundModuleInstalled = true;
            }
            else if (result.Contains("Command Not Found module is not registered in the profile file."))
            {
                IsCommandNotFoundModuleInstalled = false;
            }
        }

        public void InstallPowerShell7()
        {
            var arguments = $"-NoProfile -Command \"winget install --id Microsoft.Powershell --source winget\"";
            var result = RunPowerShellScript("powershell.exe", arguments);
            if (result.Contains("Successfully installed"))
            {
                IsPowerShell7Detected = true;
            }
        }

        public void InstallWinGetClientModule()
        {
            var ps1File = AssemblyDirectory + "\\Assets\\Settings\\Scripts\\InstallWinGetClientModule.ps1";
            var arguments = $"-NoProfile -ExecutionPolicy Unrestricted -File \"{ps1File}\"";
            var result = RunPowerShellScript("pwsh.exe", arguments);
            if (result.Contains("WinGet Client module detected."))
            {
                IsWinGetClientModuleDetected = true;
            }
            else if (result.Contains("WinGet Client module not detected."))
            {
                IsWinGetClientModuleDetected = false;
            }
        }

        public void InstallModule()
        {
            var ps1File = AssemblyDirectory + "\\Assets\\Settings\\Scripts\\EnableModule.ps1";
            var arguments = $"-NoProfile -ExecutionPolicy Unrestricted -File \"{ps1File}\" -scriptPath \"{AssemblyDirectory}\\..\"";
            var result = RunPowerShellScript("pwsh.exe", arguments);

            if (result.Contains("Module is already registered in the profile file.") || result.Contains("Module was successfully registered in the profile file."))
            {
                IsCommandNotFoundModuleInstalled = true;
                PowerToysTelemetry.Log.WriteEvent(new CmdNotFoundInstallEvent());
            }
        }

        public void UninstallModule()
        {
            var ps1File = AssemblyDirectory + "\\Assets\\Settings\\Scripts\\DisableModule.ps1";
            var arguments = $"-NoProfile -ExecutionPolicy Unrestricted -File \"{ps1File}\"";
            var result = RunPowerShellScript("pwsh.exe", arguments);

            if (result.Contains("Removed the Command Not Found reference from the profile file.") || result.Contains("No instance of Command Not Found was found in the profile file."))
            {
                IsCommandNotFoundModuleInstalled = false;
                PowerToysTelemetry.Log.WriteEvent(new CmdNotFoundUninstallEvent());
            }
        }
     }
}