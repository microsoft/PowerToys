// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.Win32;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Listens for LightSwitch theme change events and notifies subscribers.
    /// Encapsulates all LightSwitch integration logic including:
    /// - Background thread management for event listening
    /// - LightSwitch settings file parsing
    /// - System theme detection
    /// </summary>
    public sealed partial class LightSwitchListener : IDisposable
    {
        private const string LogPrefix = "[LightSwitch Integration]";

        private Thread? _listenerThread;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _disposed;

        /// <summary>
        /// Fired when LightSwitch signals a theme change and a profile should be applied
        /// </summary>
        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        /// <summary>
        /// Starts the background thread to listen for LightSwitch theme change events
        /// </summary>
        public void Start()
        {
            if (_listenerThread != null && _listenerThread.IsAlive)
            {
                Logger.LogWarning($"{LogPrefix} Listener already running");
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            _listenerThread = new Thread(() => ListenerThreadProc(token))
            {
                IsBackground = true,
                Name = "LightSwitchEventListener",
            };

            _listenerThread.Start();
            Logger.LogInfo($"{LogPrefix} Listener started");
        }

        /// <summary>
        /// Stops the background listener thread
        /// </summary>
        public void Stop()
        {
            if (_cancellationTokenSource == null)
            {
                return;
            }

            _cancellationTokenSource.Cancel();

            if (_listenerThread != null && _listenerThread.IsAlive)
            {
                try
                {
                    if (!_listenerThread.Join(TimeSpan.FromSeconds(2)))
                    {
                        Logger.LogWarning($"{LogPrefix} Listener thread did not stop in time");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogDebug($"{LogPrefix} Error joining listener thread: {ex.Message}");
                }
            }

            _listenerThread = null;
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;

            Logger.LogInfo($"{LogPrefix} Listener stopped");
        }

        private void ListenerThreadProc(CancellationToken cancellationToken)
        {
            try
            {
                Logger.LogInfo($"{LogPrefix} Event listener thread started");

                using var themeChangedEvent = new EventWaitHandle(false, EventResetMode.AutoReset, PathConstants.LightSwitchThemeChangedEventName);

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Wait for LightSwitch to signal theme change (with timeout to allow cancellation check)
                    if (themeChangedEvent.WaitOne(TimeSpan.FromSeconds(1)))
                    {
                        Logger.LogInfo($"{LogPrefix} Theme change event received");

                        // Process the theme change
                        _ = Task.Run(() => ProcessThemeChangeAsync(), CancellationToken.None);
                    }
                }

                Logger.LogInfo($"{LogPrefix} Event listener thread stopping");
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Event listener thread failed: {ex.Message}");
            }
        }

        private async Task ProcessThemeChangeAsync()
        {
            try
            {
                Logger.LogInfo($"{LogPrefix} Processing theme change");

                var result = ReadLightSwitchSettings();

                if (result == null)
                {
                    // Settings not found or integration disabled
                    return;
                }

                var (isLightMode, profileToApply) = result.Value;

                if (string.IsNullOrEmpty(profileToApply) || profileToApply == "(None)")
                {
                    Logger.LogInfo($"{LogPrefix} No profile configured for {(isLightMode ? "light" : "dark")} mode");
                    return;
                }

                Logger.LogInfo($"{LogPrefix} Requesting profile application: {profileToApply}");

                // Notify subscribers
                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(isLightMode, profileToApply));
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Failed to process theme change: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Reads LightSwitch settings and determines which profile to apply
        /// </summary>
        /// <returns>Tuple of (isLightMode, profileName) or null if integration is disabled/unavailable</returns>
        private static (bool IsLightMode, string? ProfileToApply)? ReadLightSwitchSettings()
        {
            try
            {
                var settingsPath = PathConstants.LightSwitchSettingsFilePath;

                if (!File.Exists(settingsPath))
                {
                    Logger.LogWarning($"{LogPrefix} LightSwitch settings file not found");
                    return null;
                }

                var json = File.ReadAllText(settingsPath);
                var settings = JsonDocument.Parse(json);
                var root = settings.RootElement;

                if (!root.TryGetProperty("properties", out var properties))
                {
                    Logger.LogWarning($"{LogPrefix} LightSwitch settings has no properties");
                    return null;
                }

                // Check if monitor settings integration is enabled
                if (!properties.TryGetProperty("apply_monitor_settings", out var applyMonitorSettingsElement) ||
                    !applyMonitorSettingsElement.TryGetProperty("value", out var applyValue) ||
                    !applyValue.GetBoolean())
                {
                    Logger.LogInfo($"{LogPrefix} Monitor settings integration is disabled");
                    return null;
                }

                // Determine current theme
                bool isLightMode = IsSystemInLightMode();
                Logger.LogInfo($"{LogPrefix} Current system theme: {(isLightMode ? "Light" : "Dark")}");

                // Get the appropriate profile name
                string? profileToApply = null;

                if (isLightMode)
                {
                    profileToApply = GetProfileFromSettings(properties, "enable_light_mode_profile", "light_mode_profile");
                }
                else
                {
                    profileToApply = GetProfileFromSettings(properties, "enable_dark_mode_profile", "dark_mode_profile");
                }

                return (isLightMode, profileToApply);
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Failed to read LightSwitch settings: {ex.Message}");
                return null;
            }
        }

        private static string? GetProfileFromSettings(
            JsonElement properties,
            string enableKey,
            string profileKey)
        {
            if (properties.TryGetProperty(enableKey, out var enableElement) &&
                enableElement.TryGetProperty("value", out var enableValue) &&
                enableValue.GetBoolean() &&
                properties.TryGetProperty(profileKey, out var profileElement) &&
                profileElement.TryGetProperty("value", out var profileValue))
            {
                return profileValue.GetString();
            }

            return null;
        }

        /// <summary>
        /// Check if Windows is currently in light mode
        /// </summary>
        public static bool IsSystemInLightMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    var value = key.GetValue("SystemUsesLightTheme");
                    if (value is int intValue)
                    {
                        return intValue == 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Failed to read system theme: {ex.Message}");
            }

            return false; // Default to dark mode
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
