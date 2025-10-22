// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using ModelContextProtocol.Server;
using Lock = System.Threading.Lock;

namespace PowerToys.McpServer.Tools
{
    /// <summary>
    /// MCP tools for PowerToys Awake module.
    /// </summary>
    [McpServerToolType]
    public static class AwakeTools
    {
        private static readonly SettingsUtils SettingsUtils = new SettingsUtils();
        private const string PowerToysProcessName = "PowerToys";
        private const string AwakeExecutableName = "PowerToys.Awake.exe";
        private static readonly string[] AwakeRelativeSearchPaths =
        [
            AwakeExecutableName,
            Path.Combine("modules", "Awake", AwakeExecutableName),
        ];

        /// <summary>
        /// Gets the current Awake mode and configuration.
        /// </summary>
        /// <returns>JSON object with current Awake status.</returns>
        [McpServerTool]
        [Description("Get the current Awake mode and configuration from the PowerToys settings store.")]
        public static JsonObject GetAwakeStatus()
        {
            try
            {
                (bool powerToysRunning, bool awakeModuleEnabled) = CheckPowerToysAndAwakeStatus();

                if (!powerToysRunning || !awakeModuleEnabled)
                {
                    if (IsAwakeProcessRunning())
                    {
                        // Awake is running via CLI, but we cannot determine its actual configuration
                        Logger.LogInfo("[MCP] Detected Awake CLI process running while PowerToys is not active or Awake module is disabled.");
                        return AwakeStatusPayload.CreateUnknownActive().ToJsonObject();
                    }

                    return AwakeStatusPayload.CreateInactive().ToJsonObject();
                }

                // PowerToys is running and Awake module is enabled
                bool awakeProcessRunning = IsAwakeProcessRunning();

                AwakeSettings settings = SettingsUtils.GetSettingsOrDefault<AwakeSettings>(AwakeSettings.ModuleName);
                string summary = FormatAwakeDescription(settings);

                if (awakeProcessRunning)
                {
                    summary = $"{summary} An Awake process is already running with the current configuration. To override the active session and apply new settings, use force=true.";
                }

                AwakeStatusPayload payload = AwakeStatusPayload.FromSettings(settings, summary);
                Logger.LogInfo("[MCP] Retrieved Awake status via SDK tool.");
                return payload.ToJsonObject();
            }
            catch (Exception ex)
            {
                Logger.LogError("[MCP] Failed to read Awake status.", ex);
                return AwakeStatusPayload.CreateError(ex.Message).ToJsonObject();
            }
        }

        /// <summary>
        /// Sets the Awake mode to passive (allow system sleep).
        /// </summary>
        /// <returns>JSON object with updated Awake status.</returns>
        [McpServerTool]
        [Description("Set Awake to passive mode (allow system to sleep normally).")]
        public static JsonObject SetAwakePassive()
        {
            try
            {
                (bool powerToysRunning, bool awakeModuleEnabled) = CheckPowerToysAndAwakeStatus();

                if (!powerToysRunning || !awakeModuleEnabled)
                {
                    StopAwakeProcesses();
                    Logger.LogInfo("[MCP] Stopped all Awake processes because PowerToys is not running.");
                    return AwakeStatusPayload.CreateInactive().ToJsonObject();
                }

                AwakeSettings settings = SettingsUtils.GetSettingsOrDefault<AwakeSettings>(AwakeSettings.ModuleName);
                settings.Properties.Mode = AwakeMode.PASSIVE;
                settings.Properties.KeepDisplayOn = false;
                settings.Properties.IntervalHours = 0;
                settings.Properties.IntervalMinutes = 0;
                SettingsUtils.SaveSettings(settings.ToJsonString(), AwakeSettings.ModuleName);

                string confirmation = FormatAwakeDescription(settings);
                Logger.LogInfo($"[MCP] {confirmation}");
                AwakeStatusPayload payload = AwakeStatusPayload.FromSettings(settings, confirmation);
                return payload.ToJsonObject();
            }
            catch (Exception ex)
            {
                Logger.LogError("[MCP] Failed to set Awake to passive.", ex);
                return AwakeStatusPayload.CreateError(ex.Message).ToJsonObject();
            }
        }

        /// <summary>
        /// Sets the Awake mode to indefinite (keep system awake forever).
        /// </summary>
        /// <param name="keepDisplayOn">Whether to keep the display on. Default is true.</param>
        /// <returns>JSON object with updated Awake status.</returns>
        [McpServerTool]
        [Description("Set Awake to indefinite mode (keep system awake until manually changed).")]
        public static JsonObject SetAwakeIndefinite(
            [Description("Whether to keep the display on")] bool keepDisplayOn = true,
            [Description("Force the change even if Awake is already running (default: false)")] bool force = false)
        {
            try
            {
                (bool powerToysRunning, bool awakeModuleEnabled) = CheckPowerToysAndAwakeStatus();

                if (!powerToysRunning || !awakeModuleEnabled)
                {
                    return AwakeStatusPayload.CreateError(
                        "Indefinite mode requires PowerToys to be running with Awake module enabled. CLI mode does not support indefinite operation.").ToJsonObject();
                }

                AwakeSettings settings = SettingsUtils.GetSettingsOrDefault<AwakeSettings>(AwakeSettings.ModuleName);
                if (!force && IsAwakeActive(settings))
                {
                    return BuildActiveProcessResponse(settings, true, false);
                }

                settings.Properties.Mode = AwakeMode.INDEFINITE;
                settings.Properties.KeepDisplayOn = keepDisplayOn;
                settings.Properties.IntervalHours = 0;
                settings.Properties.IntervalMinutes = 0;
                SettingsUtils.SaveSettings(settings.ToJsonString(), AwakeSettings.ModuleName);

                string confirmation = FormatAwakeDescription(settings);
                Logger.LogInfo($"[MCP] {confirmation}");
                AwakeStatusPayload payload = AwakeStatusPayload.FromSettings(settings, confirmation);
                return payload.ToJsonObject();
            }
            catch (Exception ex)
            {
                Logger.LogError("[MCP] Failed to set Awake to indefinite.", ex);
                return AwakeStatusPayload.CreateError(ex.Message).ToJsonObject();
            }
        }

        /// <summary>
        /// Sets the Awake mode to expire at a specific date and time.
        /// </summary>
        /// <param name="expireAt">ISO 8601 date/time when Awake should expire (e.g., "2025-10-22T15:30:00").</param>
        /// <param name="keepDisplayOn">Whether to keep the display on. Default is true.</param>
        /// <returns>JSON object with updated Awake status.</returns>
        [McpServerTool]
        [Description("Set Awake to expire at a specific date and time (ISO 8601 format).")]
        public static JsonObject SetAwakeExpireAt(
            [Description("ISO 8601 date/time when Awake should expire (e.g., \"2025-10-22T15:30:00\")")] string expireAt,
            [Description("Whether to keep the display on")] bool keepDisplayOn = true,
            [Description("Force the change even if Awake is already running (default: false)")] bool force = false)
        {
            try
            {
                if (!DateTimeOffset.TryParse(expireAt, out DateTimeOffset expirationDateTime))
                {
                    return AwakeStatusPayload.CreateError($"Invalid date format: '{expireAt}'. Please use ISO 8601 format (e.g., '2025-10-22T15:30:00').").ToJsonObject();
                }

                if (expirationDateTime <= DateTimeOffset.Now)
                {
                    return AwakeStatusPayload.CreateError("Expiration time must be in the future.").ToJsonObject();
                }

                (bool powerToysRunning, bool awakeModuleEnabled) = CheckPowerToysAndAwakeStatus();

                if (!powerToysRunning || !awakeModuleEnabled)
                {
                    TimeSpan duration = expirationDateTime - DateTimeOffset.Now;
                    uint durationSeconds = (uint)Math.Max(60, duration.TotalSeconds);
                    return HandleCliScenario(AwakeMode.EXPIRABLE, keepDisplayOn, durationSeconds, force);
                }

                AwakeSettings settings = SettingsUtils.GetSettingsOrDefault<AwakeSettings>(AwakeSettings.ModuleName);
                if (!force && IsAwakeActive(settings))
                {
                    return BuildActiveProcessResponse(settings, true, false);
                }

                TimeSpan timeSpan = expirationDateTime - DateTimeOffset.Now;
                uint hours = (uint)timeSpan.TotalHours;
                uint minutes = (uint)Math.Ceiling(timeSpan.TotalMinutes % 60);
                if (hours == 0 && minutes == 0)
                {
                    minutes = 1;
                }

                settings.Properties.Mode = AwakeMode.EXPIRABLE;
                settings.Properties.KeepDisplayOn = keepDisplayOn;
                settings.Properties.IntervalHours = hours;
                settings.Properties.IntervalMinutes = minutes;
                settings.Properties.ExpirationDateTime = expirationDateTime;
                SettingsUtils.SaveSettings(settings.ToJsonString(), AwakeSettings.ModuleName);

                string confirmation = FormatAwakeDescription(settings);
                Logger.LogInfo($"[MCP] {confirmation}");
                AwakeStatusPayload payload = AwakeStatusPayload.FromSettings(settings, confirmation);
                return payload.ToJsonObject();
            }
            catch (Exception ex)
            {
                Logger.LogError("[MCP] Failed to set Awake expire-at mode.", ex);
                return AwakeStatusPayload.CreateError(ex.Message).ToJsonObject();
            }
        }

        /// <summary>
        /// Sets the Awake mode to timed (keep system awake for a specific duration).
        /// </summary>
        /// <param name="durationSeconds">Duration in seconds (minimum 60).</param>
        /// <param name="keepDisplayOn">Whether to keep the display on. Default is true.</param>
        /// <returns>JSON object with updated Awake status.</returns>
        [McpServerTool]
        [Description("Set Awake to timed mode (keep system awake for a specific duration).")]
        public static JsonObject SetAwakeTimed(
            [Description("Duration in seconds (minimum 60)")] int durationSeconds,
            [Description("Whether to keep the display on")] bool keepDisplayOn = true,
            [Description("Force the change even if Awake is already running (default: false)")] bool force = false)
        {
            try
            {
                if (durationSeconds < 60)
                {
                    durationSeconds = 60;
                }

                (bool powerToysRunning, bool awakeModuleEnabled) = CheckPowerToysAndAwakeStatus();

                if (!powerToysRunning || !awakeModuleEnabled)
                {
                    return HandleCliScenario(AwakeMode.TIMED, keepDisplayOn, (uint)durationSeconds, force);
                }

                TimeSpan timeSpan = TimeSpan.FromSeconds(durationSeconds);
                uint hours = (uint)timeSpan.TotalHours;
                uint minutes = (uint)Math.Ceiling(timeSpan.TotalMinutes % 60);
                if (hours == 0 && minutes == 0)
                {
                    minutes = 1;
                }

                AwakeSettings settings = SettingsUtils.GetSettingsOrDefault<AwakeSettings>(AwakeSettings.ModuleName);
                if (!force && IsAwakeActive(settings))
                {
                    return BuildActiveProcessResponse(settings, true, false);
                }

                settings.Properties.Mode = AwakeMode.TIMED;
                settings.Properties.KeepDisplayOn = keepDisplayOn;
                settings.Properties.IntervalHours = hours;
                settings.Properties.IntervalMinutes = minutes;
                settings.Properties.ExpirationDateTime = DateTimeOffset.Now.Add(timeSpan);
                SettingsUtils.SaveSettings(settings.ToJsonString(), AwakeSettings.ModuleName);

                string confirmation = FormatAwakeDescription(settings);
                Logger.LogInfo($"[MCP] {confirmation}");
                AwakeStatusPayload payload = AwakeStatusPayload.FromSettings(settings, confirmation);
                return payload.ToJsonObject();
            }
            catch (Exception ex)
            {
                Logger.LogError("[MCP] Failed to set Awake to timed mode.", ex);
                return AwakeStatusPayload.CreateError(ex.Message).ToJsonObject();
            }
        }

        private static string FormatAwakeDescription(AwakeSettings settings)
        {
            var mode = settings.Properties.Mode.ToString().ToLowerInvariant();
            var display = settings.Properties.KeepDisplayOn ? "display on" : "display off";

            return settings.Properties.Mode switch
            {
                AwakeMode.PASSIVE => "Awake mode: passive (system sleep allowed)",
                AwakeMode.INDEFINITE => $"Awake mode: indefinite, {display}",
                AwakeMode.TIMED => $"Awake mode: timed ({settings.Properties.IntervalHours}h {settings.Properties.IntervalMinutes}m), {display}",
                AwakeMode.EXPIRABLE => $"Awake mode: expirable (until {settings.Properties.ExpirationDateTime:yyyy-MM-dd HH:mm}), {display}",
                _ => $"Awake mode: {mode}",
            };
        }

        private static JsonObject BuildActiveProcessResponse(AwakeSettings settings, bool powerToysRunning, bool launchedViaCli)
        {
            return AwakeStatusPayload.CreateError(
                "Awake is already running. Use force=true to override.",
                settings).ToJsonObject();
        }

        private static bool IsPowerToysRunning()
        {
            try
            {
                return Process.GetProcessesByName(PowerToysProcessName).Length > 0;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[MCP] Unable to determine PowerToys runner status: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Gets whether the Awake module is enabled in PowerToys settings.
        /// </summary>
        /// <returns>True if Awake module is enabled, false otherwise</returns>
        private static bool IsAwakeModuleEnabled()
        {
            try
            {
                var generalSettings = SettingsUtils.GetSettings<GeneralSettings>();
                return generalSettings?.Enabled?.Awake == true;
            }
            catch
            {
                // If we can't read settings, assume disabled
                return false;
            }
        }

        /// <summary>
        /// Checks PowerToys and Awake module status.
        /// </summary>
        /// <returns>Tuple containing (powerToysRunning, awakeModuleEnabled)</returns>
        private static (bool PowerToysRunning, bool AwakeModuleEnabled) CheckPowerToysAndAwakeStatus()
        {
            bool powerToysRunning = IsPowerToysRunning();
            bool awakeModuleEnabled = powerToysRunning && IsAwakeModuleEnabled();

            return (powerToysRunning, awakeModuleEnabled);
        }

        /// <summary>
        /// Handles CLI scenario when PowerToys is not running or Awake module is disabled.
        /// </summary>
        /// <param name="mode">The Awake mode to set</param>
        /// <param name="keepDisplayOn">Whether to keep display on</param>
        /// <param name="durationSeconds">Duration in seconds (0 for indefinite)</param>
        /// <param name="force">Whether to force override existing process</param>
        /// <returns>JSON response for CLI scenario</returns>
        private static JsonObject HandleCliScenario(AwakeMode mode, bool keepDisplayOn, uint durationSeconds, bool force)
        {
            if (!force && IsAwakeProcessRunning())
            {
                return AwakeStatusPayload.CreateError(
                    "Awake is already running and PowerToys is not active. Use force=true to override.").ToJsonObject();
            }

            if (IsAwakeProcessRunning())
            {
                StopAwakeProcesses();
            }

            JsonObject cliPayload = StartAwakeCliProcess(mode, keepDisplayOn, durationSeconds);
            return cliPayload;
        }

        private static JsonObject StartAwakeCliProcess(AwakeMode mode, bool keepDisplayOn, uint durationSeconds)
        {
            try
            {
                if (!TryResolveAwakeExecutable(out string executablePath))
                {
                    throw new FileNotFoundException("PowerToys.Awake.exe was not found near the MCP server executable.");
                }

                ProcessStartInfo startInfo = CreateSimpleStartInfo(executablePath, mode, keepDisplayOn, durationSeconds);
                Process? launchedProcess = Process.Start(startInfo);
                if (launchedProcess is null)
                {
                    throw new InvalidOperationException("Failed to start PowerToys.Awake.exe.");
                }

                // No tracking, just launch and forget
                launchedProcess.Dispose();

                AwakeSettings snapshot = BuildAwakeSnapshot(mode, keepDisplayOn, durationSeconds);
                string confirmation = FormatAwakeDescription(snapshot);
                Logger.LogInfo($"[MCP] Launched Awake CLI for mode {mode} (PowerToys not running).");
                AwakeStatusPayload payload = AwakeStatusPayload.FromSettings(snapshot, confirmation);
                return payload.ToJsonObject();
            }
            catch (Exception ex)
            {
                Logger.LogError("[MCP] Failed to start Awake CLI.", ex);
                return AwakeStatusPayload.CreateError(ex.Message).ToJsonObject();
            }
        }

        private static ProcessStartInfo CreateSimpleStartInfo(string executablePath, AwakeMode mode, bool keepDisplayOn, uint durationSeconds)
        {
            string workingDirectory = Path.GetDirectoryName(executablePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? AppDomain.CurrentDomain.BaseDirectory : workingDirectory,
            };

            startInfo.ArgumentList.Add("--display-on");
            startInfo.ArgumentList.Add(keepDisplayOn ? "true" : "false");

            if (mode == AwakeMode.TIMED && durationSeconds > 0)
            {
                startInfo.ArgumentList.Add("--time-limit");
                startInfo.ArgumentList.Add(durationSeconds.ToString(CultureInfo.InvariantCulture));
            }
            else if (mode == AwakeMode.EXPIRABLE && durationSeconds > 0)
            {
                // For EXPIRABLE mode, convert duration to expiration datetime
                DateTimeOffset expirationDateTime = DateTimeOffset.Now.AddSeconds(durationSeconds);
                startInfo.ArgumentList.Add("--expire-at");
                startInfo.ArgumentList.Add(expirationDateTime.ToString("O")); // ISO 8601 format
            }

            return startInfo;
        }

        private static void StopAwakeProcesses()
        {
            string processName = Path.GetFileNameWithoutExtension(AwakeExecutableName);
            try
            {
                Process[] awakeProcesses = Process.GetProcessesByName(processName);
                foreach (Process process in awakeProcesses)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(true);
                            process.WaitForExit(TimeSpan.FromSeconds(5));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"[MCP] Failed to terminate Awake process {process.Id}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[MCP] Failed to enumerate Awake processes: {ex.Message}");
            }
        }

        private static AwakeSettings BuildAwakeSnapshot(AwakeMode mode, bool keepDisplayOn, uint durationSeconds)
        {
            var snapshot = new AwakeSettings();
            snapshot.Properties.Mode = mode;
            snapshot.Properties.KeepDisplayOn = keepDisplayOn;

            if (mode == AwakeMode.TIMED && durationSeconds > 0)
            {
                TimeSpan timeSpan = TimeSpan.FromSeconds(durationSeconds);
                snapshot.Properties.IntervalHours = (uint)timeSpan.TotalHours;
                snapshot.Properties.IntervalMinutes = (uint)Math.Ceiling(timeSpan.TotalMinutes % 60);
                snapshot.Properties.ExpirationDateTime = DateTimeOffset.Now.Add(timeSpan);
            }
            else if (mode == AwakeMode.EXPIRABLE && durationSeconds > 0)
            {
                snapshot.Properties.IntervalHours = 0;
                snapshot.Properties.IntervalMinutes = 0;
                snapshot.Properties.ExpirationDateTime = DateTimeOffset.Now.AddSeconds(durationSeconds);
            }
            else
            {
                snapshot.Properties.IntervalHours = 0;
                snapshot.Properties.IntervalMinutes = 0;
                snapshot.Properties.ExpirationDateTime = DateTimeOffset.Now;
            }

            return snapshot;
        }

        private static bool TryResolveAwakeExecutable(out string executablePath)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (TryResolveAwakeExecutableFrom(baseDirectory, out executablePath))
            {
                return true;
            }

            string? parentDirectory = Directory.GetParent(baseDirectory)?.FullName;
            if (!string.IsNullOrEmpty(parentDirectory) && TryResolveAwakeExecutableFrom(parentDirectory, out executablePath))
            {
                return true;
            }

            executablePath = string.Empty;
            return false;
        }

        private static bool TryResolveAwakeExecutableFrom(string rootDirectory, out string executablePath)
        {
            foreach (string relativePath in AwakeRelativeSearchPaths)
            {
                string candidate = Path.Combine(rootDirectory, relativePath);
                if (File.Exists(candidate))
                {
                    executablePath = candidate;
                    return true;
                }
            }

            executablePath = string.Empty;
            return false;
        }

        private static bool IsAwakeProcessRunning()
        {
            try
            {
                string processName = Path.GetFileNameWithoutExtension(AwakeExecutableName);
                return Process.GetProcessesByName(processName).Length > 0;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[MCP] Unable to determine Awake process status: {ex.Message}");
                return false;
            }
        }

        private static bool IsAwakeActive(AwakeSettings settings)
        {
            // Only check if Awake module is enabled
            return IsAwakeModuleEnabled();
        }

        private sealed class AwakeStatusPayload
        {
            internal string Mode { get; set; } = "unknown";

            internal bool? KeepDisplayOn { get; set; }

            internal uint? IntervalHours { get; set; }

            internal uint? IntervalMinutes { get; set; }

            internal string? ExpirationDateTime { get; set; }

            internal string Summary { get; set; } = string.Empty;

            internal bool Success { get; set; } = true;

            internal string? ErrorMessage { get; set; }

            internal JsonObject ToJsonObject()
            {
                var result = new JsonObject
                {
                    ["mode"] = Mode,
                    ["summary"] = Summary,
                };

                // Add properties only if they have values
                if (KeepDisplayOn.HasValue)
                {
                    result["keepDisplayOn"] = KeepDisplayOn.Value;
                }

                if (IntervalHours.HasValue)
                {
                    result["intervalHours"] = IntervalHours.Value;
                }

                if (IntervalMinutes.HasValue)
                {
                    result["intervalMinutes"] = IntervalMinutes.Value;
                }

                if (!string.IsNullOrEmpty(ExpirationDateTime))
                {
                    result["expirationDateTime"] = ExpirationDateTime;
                }

                // Add error handling properties
                if (!Success)
                {
                    result["success"] = false;
                    if (!string.IsNullOrEmpty(ErrorMessage))
                    {
                        result["error"] = ErrorMessage;
                    }
                }

                return result;
            }

            internal static AwakeStatusPayload FromSettings(AwakeSettings settings, string summary)
            {
                var payload = new AwakeStatusPayload
                {
                    Mode = settings.Properties.Mode.ToString().ToLowerInvariant(),
                    Summary = summary,
                };

                // Only include properties relevant to the current mode
                if (settings.Properties.Mode != AwakeMode.PASSIVE)
                {
                    payload.KeepDisplayOn = settings.Properties.KeepDisplayOn;
                }

                if (settings.Properties.Mode == AwakeMode.TIMED || settings.Properties.Mode == AwakeMode.EXPIRABLE)
                {
                    payload.IntervalHours = settings.Properties.IntervalHours;
                    payload.IntervalMinutes = settings.Properties.IntervalMinutes;
                    payload.ExpirationDateTime = settings.Properties.ExpirationDateTime.ToString("O");
                }

                return payload;
            }

            internal static AwakeStatusPayload CreateInactive()
            {
                return new AwakeStatusPayload
                {
                    Mode = "inactive",
                    Summary = "PowerToys Awake is not running because PowerToys is not active.",
                };
            }

            internal static AwakeStatusPayload CreateUnknownActive()
            {
                return new AwakeStatusPayload
                {
                    Mode = "unknown",
                    Summary = "An Awake process is currently running, but its configuration cannot be determined. To terminate the existing process and start with new settings, use force=true.",
                };
            }

            internal static AwakeStatusPayload CreateError(string errorMessage, AwakeSettings? settings = null)
            {
                var payload = new AwakeStatusPayload
                {
                    Success = false,
                    ErrorMessage = errorMessage,
                };

                if (settings != null)
                {
                    payload.Mode = settings.Properties.Mode.ToString().ToLowerInvariant();

                    // Only include properties relevant to the current mode
                    if (settings.Properties.Mode != AwakeMode.PASSIVE)
                    {
                        payload.KeepDisplayOn = settings.Properties.KeepDisplayOn;
                    }

                    if (settings.Properties.Mode == AwakeMode.TIMED || settings.Properties.Mode == AwakeMode.EXPIRABLE)
                    {
                        payload.IntervalHours = settings.Properties.IntervalHours;
                        payload.IntervalMinutes = settings.Properties.IntervalMinutes;
                        payload.ExpirationDateTime = settings.Properties.ExpirationDateTime.ToString("O");
                    }

                    payload.Summary = "An Awake session is already active with the current settings. To override and change the configuration, use force=true.";
                }
                else
                {
                    payload.Mode = "unknown";
                    payload.Summary = "An Awake process is currently running. To terminate the existing process and start with new settings, use force=true.";
                }

                return payload;
            }
        }
    }
}
