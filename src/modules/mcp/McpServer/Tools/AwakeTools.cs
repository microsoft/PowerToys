// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using ModelContextProtocol.Server;

namespace PowerToys.McpServer.Tools
{
    /// <summary>
    /// MCP tools for PowerToys Awake module.
    /// </summary>
    [McpServerToolType]
    public static class AwakeTools
    {
        private static readonly SettingsUtils SettingsUtils = new SettingsUtils();

        /// <summary>
        /// Check if Awake module is enabled in MCP settings.
        /// </summary>
        private static void CheckModuleEnabled()
        {
            try
            {
                McpSettings mcpSettings = SettingsUtils.GetSettingsOrDefault<McpSettings>(McpSettings.ModuleName);
                bool isEnabled = mcpSettings.Properties.EnabledModules.TryGetValue("Awake", out bool enabled) ? enabled : true;
                if (!isEnabled)
                {
                    throw new InvalidOperationException("Awake module is disabled in MCP settings. Enable it in PowerToys Settings > MCP > Module Toggles.");
                }
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                // If we can't read MCP settings, assume enabled (backward compatibility)
                Logger.LogWarning($"[MCP] Could not check MCP module status, assuming enabled: {ex.Message}");
            }
        }

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
                CheckModuleEnabled();
                AwakeSettings settings = SettingsUtils.GetSettingsOrDefault<AwakeSettings>(AwakeSettings.ModuleName);
                string summary = FormatAwakeDescription(settings);
                JsonObject payload = FormatAwakeJson(settings, summary);
                Logger.LogInfo("[MCP] Retrieved Awake status via SDK tool.");
                return payload;
            }
            catch (Exception ex)
            {
                Logger.LogError("[MCP] Failed to read Awake status.", ex);
                return new JsonObject
                {
                    ["error"] = ex.Message,
                    ["mode"] = "unknown",
                };
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
                CheckModuleEnabled();
                AwakeSettings settings = SettingsUtils.GetSettingsOrDefault<AwakeSettings>(AwakeSettings.ModuleName);
                settings.Properties.Mode = AwakeMode.PASSIVE;
                settings.Properties.ProcessId = 0;
                settings.Properties.KeepDisplayOn = false;
                settings.Properties.IntervalHours = 0;
                settings.Properties.IntervalMinutes = 0;
                SettingsUtils.SaveSettings(settings.ToJsonString(), AwakeSettings.ModuleName);

                string confirmation = FormatAwakeDescription(settings);
                Logger.LogInfo($"[MCP] {confirmation}");
                return FormatAwakeJson(settings, confirmation);
            }
            catch (Exception ex)
            {
                Logger.LogError("[MCP] Failed to set Awake to passive.", ex);
                return new JsonObject
                {
                    ["error"] = ex.Message,
                    ["success"] = false,
                };
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
            [Description("Whether to keep the display on")] bool keepDisplayOn = true)
        {
            try
            {
                CheckModuleEnabled();
                AwakeSettings settings = SettingsUtils.GetSettingsOrDefault<AwakeSettings>(AwakeSettings.ModuleName);
                settings.Properties.Mode = AwakeMode.INDEFINITE;
                settings.Properties.ProcessId = 0;
                settings.Properties.KeepDisplayOn = keepDisplayOn;
                settings.Properties.IntervalHours = 0;
                settings.Properties.IntervalMinutes = 0;
                SettingsUtils.SaveSettings(settings.ToJsonString(), AwakeSettings.ModuleName);

                string confirmation = FormatAwakeDescription(settings);
                Logger.LogInfo($"[MCP] {confirmation}");
                return FormatAwakeJson(settings, confirmation);
            }
            catch (Exception ex)
            {
                Logger.LogError("[MCP] Failed to set Awake to indefinite.", ex);
                return new JsonObject
                {
                    ["error"] = ex.Message,
                    ["success"] = false,
                };
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
            [Description("Whether to keep the display on")] bool keepDisplayOn = true)
        {
            try
            {
                CheckModuleEnabled();
                if (durationSeconds < 60)
                {
                    durationSeconds = 60;
                }

                TimeSpan timeSpan = TimeSpan.FromSeconds(durationSeconds);
                uint hours = (uint)timeSpan.TotalHours;
                uint minutes = (uint)Math.Ceiling(timeSpan.TotalMinutes % 60);
                if (hours == 0 && minutes == 0)
                {
                    minutes = 1;
                }

                AwakeSettings settings = SettingsUtils.GetSettingsOrDefault<AwakeSettings>(AwakeSettings.ModuleName);
                settings.Properties.Mode = AwakeMode.TIMED;
                settings.Properties.ProcessId = 0;
                settings.Properties.KeepDisplayOn = keepDisplayOn;
                settings.Properties.IntervalHours = hours;
                settings.Properties.IntervalMinutes = minutes;
                settings.Properties.ExpirationDateTime = DateTimeOffset.Now.Add(timeSpan);
                SettingsUtils.SaveSettings(settings.ToJsonString(), AwakeSettings.ModuleName);

                string confirmation = FormatAwakeDescription(settings);
                Logger.LogInfo($"[MCP] {confirmation}");
                return FormatAwakeJson(settings, confirmation);
            }
            catch (Exception ex)
            {
                Logger.LogError("[MCP] Failed to set Awake to timed mode.", ex);
                return new JsonObject
                {
                    ["error"] = ex.Message,
                    ["success"] = false,
                };
            }
        }

        private static string FormatAwakeDescription(AwakeSettings settings)
        {
            var mode = settings.Properties.Mode.ToString().ToLowerInvariant();
            var display = settings.Properties.KeepDisplayOn ? "display on" : "display off";

            if (settings.Properties.ProcessId > 0)
            {
                return $"Awake mode: process-bound (PID={settings.Properties.ProcessId}), {display}";
            }

            return settings.Properties.Mode switch
            {
                AwakeMode.PASSIVE => "Awake mode: passive (system sleep allowed)",
                AwakeMode.INDEFINITE => $"Awake mode: indefinite, {display}",
                AwakeMode.TIMED => $"Awake mode: timed ({settings.Properties.IntervalHours}h {settings.Properties.IntervalMinutes}m), {display}",
                AwakeMode.EXPIRABLE => $"Awake mode: expirable (until {settings.Properties.ExpirationDateTime:yyyy-MM-dd HH:mm}), {display}",
                _ => $"Awake mode: {mode}",
            };
        }

        private static JsonObject FormatAwakeJson(AwakeSettings settings, string summary)
        {
            return new JsonObject
            {
                ["mode"] = settings.Properties.Mode.ToString().ToLowerInvariant(),
                ["keepDisplayOn"] = settings.Properties.KeepDisplayOn,
                ["isProcessBound"] = settings.Properties.ProcessId > 0,
                ["processId"] = settings.Properties.ProcessId,
                ["intervalHours"] = settings.Properties.IntervalHours,
                ["intervalMinutes"] = settings.Properties.IntervalMinutes,
                ["expirationDateTime"] = settings.Properties.ExpirationDateTime.ToString("O"),
                ["summary"] = summary,
            };
        }
    }
}
