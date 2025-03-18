// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class CmdNotFoundViewModel : Observable
    {
        public ButtonClickCommand CheckRequirementsEventHandler => new ButtonClickCommand(CheckCommandNotFoundRequirements);

        public ButtonClickCommand InstallPowerShell7EventHandler => new ButtonClickCommand(InstallPowerShell7);

        public ButtonClickCommand InstallWinGetClientModuleEventHandler => new ButtonClickCommand(InstallWinGetClientModule);

        public ButtonClickCommand InstallModuleEventHandler => new ButtonClickCommand(InstallModule);

        public ButtonClickCommand UninstallModuleEventHandler => new ButtonClickCommand(UninstallModule);

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _moduleIsGpoEnabled;
        private bool _moduleIsGpoDisabled;

        public static string AssemblyDirectory
        {
            get
            {
                return Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            }
        }

        public CmdNotFoundViewModel()
        {
            InitializeEnabledValue();
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredCmdNotFoundEnabledValue();
            _moduleIsGpoEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            _moduleIsGpoDisabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled;

            // Update PATH environment variable to get pwsh.exe on further calls.
            Environment.SetEnvironmentVariable("PATH", (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? string.Empty) + ";" + (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty), EnvironmentVariableTarget.Process);

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

        private bool isPowerShellPreviewDetected;
        private string powerShellPreviewPath;

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

        public bool IsModuleGpoEnabled
        {
            get => _moduleIsGpoEnabled;
        }

        public bool IsModuleGpoDisabled
        {
            get => _moduleIsGpoDisabled;
        }

        public string RunPowerShellOrPreviewScript(string powershellExecutable, string powershellArguments, bool hidePowerShellWindow = false)
        {
            if (isPowerShellPreviewDetected)
            {
                return RunPowerShellScript(Path.Combine(powerShellPreviewPath, "pwsh-preview.cmd"), powershellArguments, hidePowerShellWindow);
            }
            else
            {
                return RunPowerShellScript(powershellExecutable, powershellArguments, hidePowerShellWindow);
            }
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
            isPowerShellPreviewDetected = false;
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

            if (!IsPowerShell7Detected)
            {
                // powerShell Preview might be installed, check it.
                try
                {
                    // we have to search for the directory where the PowerShell preview command is located. It is added to the PATH environment variable, so we have to search for it there
                    foreach (string pathCandidate in Environment.GetEnvironmentVariable("PATH").Split(';'))
                    {
                        if (File.Exists(Path.Combine(pathCandidate, "pwsh-preview.cmd")))
                        {
                            result = RunPowerShellScript(Path.Combine(pathCandidate, "pwsh-preview.cmd"), arguments, true);
                            if (result.Contains("PowerShell 7.4 or greater detected."))
                            {
                                isPowerShellPreviewDetected = true;
                                IsPowerShell7Detected = true;
                                powerShellPreviewPath = pathCandidate;
                                break;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // nothing to do. No additional PowerShell installation found
                }
            }

            if (result.Contains("WinGet Client module detected."))
            {
                IsWinGetClientModuleDetected = true;
            }
            else if (result.Contains("WinGet Client module not detected.") || result.Contains("WinGet Client module needs to be updated."))
            {
                IsWinGetClientModuleDetected = false;
            }

            if (result.Contains("Command Not Found module is registered in the profile file."))
            {
                IsCommandNotFoundModuleInstalled = true;
            }
            else if (result.Contains("Command Not Found module is not registered in the profile file.") || result.Contains("Outdated version of Command Not Found module found in the profile file."))
            {
                IsCommandNotFoundModuleInstalled = false;
            }

            Logger.LogInfo(result);
        }

        public void InstallPowerShell7()
        {
            var ps1File = AssemblyDirectory + "\\Assets\\Settings\\Scripts\\InstallPowerShell7.ps1";
            var arguments = $"-NoProfile -ExecutionPolicy Unrestricted -File \"{ps1File}\"";
            var result = RunPowerShellOrPreviewScript("powershell.exe", arguments);
            if (result.Contains("Powershell 7 successfully installed."))
            {
                IsPowerShell7Detected = true;
            }

            Logger.LogInfo(result);

            // Update PATH environment variable to get pwsh.exe on further calls.
            Environment.SetEnvironmentVariable("PATH", (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? string.Empty) + ";" + (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty), EnvironmentVariableTarget.Process);
        }

        public void InstallWinGetClientModule()
        {
            var ps1File = AssemblyDirectory + "\\Assets\\Settings\\Scripts\\InstallWinGetClientModule.ps1";
            var arguments = $"-NoProfile -ExecutionPolicy Unrestricted -File \"{ps1File}\"";
            var result = RunPowerShellOrPreviewScript("pwsh.exe", arguments);
            if (result.Contains("WinGet Client module detected.") || result.Contains("WinGet Client module updated."))
            {
                IsWinGetClientModuleDetected = true;
            }
            else if (result.Contains("WinGet Client module not detected."))
            {
                IsWinGetClientModuleDetected = false;
            }

            Logger.LogInfo(result);
        }

        public void InstallModule()
        {
            var ps1File = AssemblyDirectory + "\\Assets\\Settings\\Scripts\\EnableModule.ps1";
            var arguments = $"-NoProfile -ExecutionPolicy Unrestricted -File \"{ps1File}\" -scriptPath \"{AssemblyDirectory}\\..\"";
            var result = RunPowerShellOrPreviewScript("pwsh.exe", arguments);

            if (result.Contains("Module is already registered in the profile file.")
                || result.Contains("Module was successfully registered in the profile file.")
                || result.Contains("Module was successfully upgraded in the profile file."))
            {
                IsCommandNotFoundModuleInstalled = true;
                PowerToysTelemetry.Log.WriteEvent(new CmdNotFoundInstallEvent());
            }

            Logger.LogInfo(result);
        }

        public void UninstallModule()
        {
            var ps1File = AssemblyDirectory + "\\Assets\\Settings\\Scripts\\DisableModule.ps1";
            var arguments = $"-NoProfile -ExecutionPolicy Unrestricted -File \"{ps1File}\"";
            var result = RunPowerShellOrPreviewScript("pwsh.exe", arguments);

            if (result.Contains("Removed the Command Not Found reference from the profile file.") || result.Contains("No instance of Command Not Found was found in the profile file."))
            {
                IsCommandNotFoundModuleInstalled = false;
                PowerToysTelemetry.Log.WriteEvent(new CmdNotFoundUninstallEvent());
            }

            Logger.LogInfo(result);
        }
    }
}
