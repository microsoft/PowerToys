// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Windows.Documents;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerToys.Interop;
using RunnerV2.Models;
using RunnerV2.properties;
using Update;

namespace RunnerV2.Helpers
{
    /// <summary>
    /// This class provides helper methods to interact with the PowerToys Settings window.
    /// </summary>
    internal static class SettingsHelper
    {
        public static bool Debugging { get; set; }

        private static readonly SettingsUtils _settingsUtils = SettingsUtils.Default;
        private static Process? _process;
        private static TwoWayPipeMessageIPCManaged? _ipc;

        public static void OpenSettingsWindow(bool showOobeWindow = false, bool showScoobeWindow = false, string? additionalArguments = null)
        {
            if (_process is not null && _ipc is not null && !_process.HasExited)
            {
                _ipc.Send($@"{{""ShowYourself"": ""{additionalArguments ?? "Dashboard"}""}}");

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

            // Arg 10: Are there additional settings window arguments
            string areThereadditionalArgs = string.IsNullOrEmpty(additionalArguments) ? "false" : "true";

            string executableArgs = $"{powerToysPipeName} {settingsPipeName} {currentProcessId} {theme} {isElevated} {isAdmin} {showOobeArg} {showScoobeArg} {areThereadditionalArgs}";

            if (!string.IsNullOrEmpty(additionalArguments))
            {
                executableArgs += $" {additionalArguments}";
            }

            Logger.LogInfo($"Starting Settings with arguments: {executableArgs}");

            _process = Process.Start(executablePath, executableArgs);

            // Initialize listening to pipes
            _ipc = new TwoWayPipeMessageIPCManaged(powerToysPipeName, settingsPipeName, OnSettingsMessageReceived);
            _ipc.Start();
        }

        public static void OnSettingsMessageReceived(string message)
        {
            if (Debugging)
            {
                Console.WriteLine("Received message from settings: " + message);
            }

            JsonDocument messageDocument = JsonDocument.Parse(message);

            foreach (var property in messageDocument.RootElement.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "action":
                        foreach (var moduleName in property.Value.EnumerateObject())
                        {
                            if (moduleName.Name == "general")
                            {
                                switch (moduleName.Value.GetProperty("action_name").GetString())
                                {
                                    case "restart_elevation":
                                        ElevationHelper.RestartScheduled = ElevationHelper.RestartScheduledMode.RestartElevatedWithOpenSettings;
                                        Runner.Close();
                                        break;
                                    case "restart_maintain_elevation":
                                        ElevationHelper.RestartScheduled = ElevationHelper.IsProcessElevated() ? ElevationHelper.RestartScheduledMode.RestartElevatedWithOpenSettings : ElevationHelper.RestartScheduledMode.RestartNonElevated;

                                        ElevationHelper.RestartIfScheudled();

                                        break;
                                    case "check_for_updates":
                                        var version = Assembly.GetExecutingAssembly().GetName().Version!;
                                        var versionString = "v" + version.Major + "." + version.Minor + "." + version.Build;
                                        UpdateSettingsHelper.TriggerUpdateCheck((version) => { PowerToys.Interop.Notifications.ShowUpdateAvailableNotification("PowerToys", Resources.UpdateNotification_Content + "\n" + versionString + " \u2192 " + version, "PTUpdateNotifyTag", Resources.UpdateNotification_UpdateNow, Resources.UpdateNotification_MoreInfo); });
                                        break;
                                    case "request_update_state_date":
                                        JsonObject response = [];
                                        response["updateStateDate"] = UpdateSettingsHelper.GetLastCheckedDate();
                                        _ipc?.Send(response.ToJsonString());
                                        break;
                                    default:
                                        Logger.LogWarning($"Received unknown general action from Settings: {moduleName.Value.GetProperty("action_name").GetString()}");
                                        break;
                                }

                                break;
                            }

                            foreach (IPowerToysModule ptModule in Runner.LoadedModules)
                            {
                                if (ptModule.Name == moduleName.Name && ptModule is IPowerToysModuleCustomActionsProvider customActionsProvider && customActionsProvider.CustomActions.TryGetValue(moduleName.Value.GetProperty("action_name").GetString() ?? string.Empty, out Action<string>? action))
                                {
                                    Logger.InitializeLogger("\\" + ptModule.Name + "\\ModuleInterface\\Logs");
                                    action(moduleName.Value.GetProperty("value").GetString() ?? string.Empty);
                                    Logger.InitializeLogger("\\RunnerLogs");
                                }
                            }
                        }

                        break;
                    case "get_all_hotkey_conflicts":
                        JsonNode hotkeyConflicts = HotkeyConflictsManager.GetHotkeyConflictsAsJson();
                        hotkeyConflicts.Root["response_type"] = "all_hotkey_conflicts";
                        _ipc?.Send(hotkeyConflicts.ToJsonString());
                        break;
                    case "check_hotkey_conflict":
                        try
                        {
                            HotkeySettings hotkey = new(
                                property.Value.GetProperty("win").GetBoolean(),
                                property.Value.GetProperty("ctrl").GetBoolean(),
                                property.Value.GetProperty("alt").GetBoolean(),
                                property.Value.GetProperty("shift").GetBoolean(),
                                property.Value.GetProperty("key").GetInt32());

                            string requestId = property.Value.GetProperty("request_id").GetString() ?? string.Empty;

                            bool hasConflict = HotkeyConflictsManager.HasConflict(hotkey);

                            JsonObject response = [];
                            response["response_type"] = "hotkey_conflict_result";
                            response["request_id"] = requestId;
                            response["has_conflict"] = hasConflict;

                            if (hasConflict)
                            {
                                List<HotkeyConflictsManager.HotkeyConflict> conflicts = HotkeyConflictsManager.GetAllConflicts(hotkey);
                                JsonArray allConflicts = [];
                                foreach (HotkeyConflictsManager.HotkeyConflict conflict in conflicts)
                                {
                                    allConflicts.Add(new JsonObject
                                    {
                                        ["module"] = conflict.ModuleName,
                                        ["hotkeyID"] = conflict.HotkeyID,
                                    });
                                }

                                response["all_conflicts"] = allConflicts;
                            }

                            _ipc?.Send(response.ToJsonString());
                        }
                        catch
                        {
                        }

                        break;

                    case "module_status":
                        GeneralSettings generalSettings = _settingsUtils.GetSettings<GeneralSettings>();
                        string nameOfModule = property.Value.EnumerateObject().First().Name;
                        bool enabled = property.Value.EnumerateObject().First().Value.GetBoolean();
                        ModuleHelper.SetIsModuleEnabled(generalSettings, ModuleHelper.GetModuleType(nameOfModule), enabled);
                        SettingsUtils.Default.SaveSettings(generalSettings.ToJsonString(), string.Empty);
                        return;
                    case "bugreport":
                        Logger.LogInfo("Starting bug report tool from Settings window");
                        TrayIconManager.ProcessTrayMenuCommand((nuint)TrayIconManager.TrayButton.ReportBug);
                        break;
                    case "bug_report_status":
                        _ipc?.Send($@"{{""bug_report_running"": {(TrayIconManager.IsBugReportToolRunning ? "true" : "false")}}}");
                        break;
                    case "killrunner":
                        Runner.Close();
                        break;
                    case "general":
                        try
                        {
                            _settingsUtils.SaveSettings(property.Value.ToString(), string.Empty);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Failed writing general settings from Settings window", ex);
                        }

                        NativeMethods.PostMessageW(Runner.RunnerHwnd, (uint)NativeMethods.WindowMessages.REFRESH_SETTINGS, 0, 0);

                        foreach (IPowerToysModule module in Runner.ModulesToLoad)
                        {
                            if (module is IPowerToysModuleSettingsChangedSubscriber settingsChangedSubscriber)
                            {
                                Logger.InitializeLogger("\\" + module.Name + "\\ModuleInterface\\Logs");
                                settingsChangedSubscriber.OnSettingsChanged();
                                Logger.InitializeLogger("\\RunnerLogs");
                            }
                        }

                        break;
                    case "powertoys":
                        foreach (var powertoysSettingsPart in property.Value.EnumerateObject())
                        {
                            if (Runner.LoadedModules.Find(m => m.Name == powertoysSettingsPart.Name) is IPowerToysModuleSettingsChangedSubscriber module && module is IPowerToysModule ptModule && ptModule.Enabled)
                            {
                                Logger.InitializeLogger("\\" + ptModule.Name + "\\ModuleInterface\\Logs");
                                SettingsUtils.Default.SaveSettings(powertoysSettingsPart.Value.ToString(), powertoysSettingsPart.Name);
                                module.OnSettingsChanged();
                                Logger.InitializeLogger("\\RunnerLogs");
                            }
                            else
                            {
                                // If no specific module was found, notify all enabled modules
                                foreach (IPowerToysModule module2 in Runner.LoadedModules.Where(m => m.Enabled))
                                {
                                    if (module2 is IPowerToysModuleSettingsChangedSubscriber settingsChangedSubscriber)
                                    {
                                        Logger.InitializeLogger("\\" + module2.Name + "\\ModuleInterface\\Logs");
                                        settingsChangedSubscriber.OnSettingsChanged();
                                        Logger.InitializeLogger("\\RunnerLogs");
                                    }
                                }
                            }

                            NativeMethods.PostMessageW(Runner.RunnerHwnd, (uint)NativeMethods.WindowMessages.REFRESH_SETTINGS, 0, 0);
                        }

                        break;
                    case "language":
                        File.WriteAllText(SettingsUtils.Default.GetSettingsFilePath(fileName: "language.json"), @$"{{""{property.Name}"": ""{property.Value}""}}");
                        break;
                    default:
                        Logger.LogWarning($"Received unknown message from Settings: {property.Name}");
                        break;
                }
            }
        }

        public static void CloseSettingsWindow()
        {
            using var closeEventWrapper = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerToysRunnerTerminateSettingsEvent());
            closeEventWrapper.Set();
        }
    }
}
