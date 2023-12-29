// Copyright (c) Microsoft Corporation
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
        public ButtonClickCommand CheckPowershellVersionEventHandler => new ButtonClickCommand(CheckPowershellVersion);

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

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public void RunPowerShellScript(string powershellArguments)
        {
            string outputLog = string.Empty;
            try
            {
                var startInfo = new ProcessStartInfo()
                {
                    FileName = "pwsh.exe",
                    Arguments = powershellArguments,
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
        }

        public void CheckPowershellVersion()
        {
            var arguments = $"-NoProfile -NonInteractive -Command $PSVersionTable";
            RunPowerShellScript(arguments);
        }

        public void InstallModule()
        {
            var ps1File = AssemblyDirectory + "\\Assets\\Settings\\Scripts\\EnableModule.ps1";
            var arguments = $"-NoProfile -ExecutionPolicy Unrestricted -File \"{ps1File}\" -scriptPath \"{AssemblyDirectory}\\..\"";
            RunPowerShellScript(arguments);
            PowerToysTelemetry.Log.WriteEvent(new CmdNotFoundInstallEvent());
        }

        public void UninstallModule()
        {
            var ps1File = AssemblyDirectory + "\\Assets\\Settings\\Scripts\\DisableModule.ps1";
            var arguments = $"-NoProfile -ExecutionPolicy Unrestricted -File \"{ps1File}\"";
            RunPowerShellScript(arguments);
            PowerToysTelemetry.Log.WriteEvent(new CmdNotFoundUninstallEvent());
        }
     }
}
