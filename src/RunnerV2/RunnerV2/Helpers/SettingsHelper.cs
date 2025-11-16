// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text.Json;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.Interop;

namespace RunnerV2.Helpers
{
    internal static class SettingsHelper
    {
        private static Process? _process;
        private static TwoWayPipeMessageIPCManaged? _ipc;
        private static SettingsUtils _settingsUtils = new();

        public static void OpenSettingsWindow(bool showOobeWindow = false, bool showScoobeWindow = false, bool showFlyout = false, Point? flyoutPosition = null, string? additionalArguments = null)
        {
            if (_process is not null && _ipc is not null && !_process.HasExited)
            {
                if (showFlyout)
                {
                    _ipc.Send(@"{""ShowYourself"": ""flyout""}");
                }
                else
                {
                    _ipc.Send($@"{{""ShowYourself"": ""{additionalArguments ?? "Dashboard"}""}}");
                }

                return;
            }

            _ipc?.End();
            _ipc = null;

            // Arg 1: Executable path
            string executablePath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException("No executable path found"), "WinUI3Apps", "PowerToys.Settings.exe");

            // Arg 2,3: Pipe names
            Pipe settingsPipe = new();
            Pipe powertoysPipe = new();

            string powerToysPipeName = @"\\.\pipe\powertoys_runner_" + Guid.NewGuid();
            string settingsPipeName = @"\\.\pipe\powertoys_settings_" + Guid.NewGuid();

            // Arg 4: Process pid
            string currentProcessId = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);

            // Arg 5: Settings theme
            string theme = Program.GeneralSettings.Theme switch
            {
                "light" => "light",
                "dark" => "dark",
                "system" when ThemeHelpers.GetAppTheme() == AppTheme.Light => "light",
                "system" when ThemeHelpers.GetAppTheme() == AppTheme.Dark => "dark",
                _ => throw new NotImplementedException(),
            };

            // Arg 6: Elevated status
            string isElevated = Program.GeneralSettings.IsElevated ? "true" : "false";

            // Arg 7: Is user an administrator
            string isAdmin = Program.GeneralSettings.IsAdmin ? "true" : "false";

            // Arg 8: Show OOBE window
            string showOobeArg = showOobeWindow ? "true" : "false";

            // Arg 9: Show SCOOBE window
            string showScoobeArg = showScoobeWindow ? "true" : "false";

            // Arg 10: Show flyout
            string showFlyoutArg = showFlyout ? "true" : "false";

            // Arg 11: Are there additional settings window arguments
            string areThereadditionalArgs = string.IsNullOrEmpty(additionalArguments) ? "false" : "true";

            // Arg 12: Are there flyout position arguments
            string areThereFlyoutPositionArgs = flyoutPosition.HasValue ? "true" : "false";

            string executableArgs = $"{powerToysPipeName} {settingsPipeName} {currentProcessId} {theme} {isElevated} {isAdmin} {showOobeArg} {showScoobeArg} {showFlyoutArg} {areThereadditionalArgs} {areThereFlyoutPositionArgs}";

            if (!string.IsNullOrEmpty(additionalArguments))
            {
                executableArgs += $" {additionalArguments}";
            }

            if (flyoutPosition is not null)
            {
                executableArgs += $" {flyoutPosition.Value.X} {flyoutPosition.Value.Y}";
            }

            _process = Process.Start(executablePath, executableArgs);

            // Initialize listening to pipes
            _ipc = new TwoWayPipeMessageIPCManaged(powerToysPipeName, settingsPipeName, OnSettingsMessageReceived);
            _ipc.Start();
        }

        private static void OnSettingsMessageReceived(string message)
        {
            JsonDocument messageDocument = JsonDocument.Parse(message);

            foreach (var property in messageDocument.RootElement.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "get_all_hotkey_conflicts":
                        // Todo: Handle hotkey conflict
                        break;
                    case "general":
                        _settingsUtils.SaveSettings(property.Value.ToString(), string.Empty);
                        foreach (IPowerToysModule module in Runner.LoadedModules)
                        {
                            module.OnSettingsChanged("general", property.Value);
                            Runner.ToggleModuleStateBasedOnEnabledProperty(module);
                        }

                        break;
                    case string s:
                        _settingsUtils.SaveSettings(property.Value.ToString(), s);

                        if (Runner.LoadedModules.Find(m => m.Name == s) is IPowerToysModule moduleFound)
                        {
                            moduleFound.OnSettingsChanged(s, property.Value);
                        }
                        else
                        {
                            // If no specific module was found, notify all enabled modules
                            foreach (IPowerToysModule module in Runner.LoadedModules.Where(m => m.Enabled))
                            {
                                module.OnSettingsChanged(s, property.Value);
                            }
                        }

                        break;
                }
            }
        }
    }
}
