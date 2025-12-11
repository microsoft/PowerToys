// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Listens for LightSwitch theme change events and notifies subscribers.
    /// Encapsulates all LightSwitch integration logic including:
    /// - Background thread management for event listening (light/dark theme events)
    /// - LightSwitch settings file parsing
    /// Theme is determined directly from which event was signaled (not from registry).
    /// </summary>
    public sealed partial class LightSwitchListener : IDisposable
    {
        private const string LogPrefix = "[LightSwitch Listener]";

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

                // Use separate events for light and dark themes to avoid race conditions
                // where we might read the registry before LightSwitch has updated it
                using var lightThemeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, PathConstants.LightSwitchLightThemeEventName);
                using var darkThemeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, PathConstants.LightSwitchDarkThemeEventName);

                var waitHandles = new WaitHandle[] { lightThemeEvent, darkThemeEvent };

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Wait for either light or dark theme event (with timeout to allow cancellation check)
                    int index = WaitHandle.WaitAny(waitHandles, TimeSpan.FromSeconds(1));

                    if (index == WaitHandle.WaitTimeout)
                    {
                        continue;
                    }

                    // Determine theme from which event was signaled
                    bool isLightMode = index == 0; // 0 = lightThemeEvent, 1 = darkThemeEvent
                    Logger.LogInfo($"{LogPrefix} Theme event received: {(isLightMode ? "Light" : "Dark")}");

                    // Process the theme change with the known theme
                    _ = Task.Run(() => ProcessThemeChange(isLightMode), CancellationToken.None);
                }

                Logger.LogInfo($"{LogPrefix} Event listener thread stopping");
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Event listener thread failed: {ex.Message}");
            }
        }

        private void ProcessThemeChange(bool isLightMode)
        {
            try
            {
                Logger.LogInfo($"{LogPrefix} Processing theme change to {(isLightMode ? "light" : "dark")} mode");

                var profileToApply = ReadProfileFromLightSwitchSettings(isLightMode);

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
        }

        /// <summary>
        /// Reads LightSwitch settings and returns the profile name to apply for the given theme.
        /// The theme is determined by which event was signaled (light or dark), not by reading the registry.
        /// </summary>
        /// <param name="isLightMode">Whether the theme is light mode (determined from the signaled event)</param>
        /// <returns>The profile name to apply, or null if not configured</returns>
        private static string? ReadProfileFromLightSwitchSettings(bool isLightMode)
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

                // Get the appropriate profile name based on the theme from the event
                if (isLightMode)
                {
                    return GetProfileFromSettings(properties, "enable_light_mode_profile", "light_mode_profile");
                }
                else
                {
                    return GetProfileFromSettings(properties, "enable_dark_mode_profile", "dark_mode_profile");
                }
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
