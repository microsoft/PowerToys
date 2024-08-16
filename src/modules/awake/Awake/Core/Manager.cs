// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Awake.Core.Models;
using Awake.Core.Native;
using Awake.Properties;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Microsoft.Win32;

namespace Awake.Core
{
    public delegate bool ConsoleEventHandler(Models.ControlType ctrlType);

    /// <summary>
    /// Helper class that allows talking to Win32 APIs without having to rely on PInvoke in other parts
    /// of the codebase.
    /// </summary>
    public class Manager
    {
        private static bool _isUsingPowerToysConfig;

        internal static bool IsUsingPowerToysConfig { get => _isUsingPowerToysConfig; set => _isUsingPowerToysConfig = value; }

        private static readonly CompositeFormat AwakeMinutes = CompositeFormat.Parse(Resources.AWAKE_MINUTES);
        private static readonly CompositeFormat AwakeHours = CompositeFormat.Parse(Resources.AWAKE_HOURS);

        private static readonly BlockingCollection<ExecutionState> _stateQueue;

        // Core icons used for the tray
        private static readonly Icon _timedIcon = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Awake/timed.ico"));
        private static readonly Icon _expirableIcon = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Awake/expirable.ico"));
        private static readonly Icon _indefiniteIcon = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Awake/indefinite.ico"));
        private static readonly Icon _disabledIcon = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Awake/disabled.ico"));

        private static CancellationTokenSource _tokenSource;

        private static SettingsUtils? _moduleSettings;

        internal static SettingsUtils? ModuleSettings { get => _moduleSettings; set => _moduleSettings = value; }

        static Manager()
        {
            _tokenSource = new CancellationTokenSource();
            _stateQueue = [];
            ModuleSettings = new SettingsUtils();
        }

        internal static void StartMonitor()
        {
            Thread monitorThread = new(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                while (true)
                {
                    ExecutionState state = _stateQueue.Take();

                    Logger.LogInfo($"Setting state to {state}");

                    SetAwakeState(state);
                }
            });
            monitorThread.Start();
        }

        internal static void SetConsoleControlHandler(ConsoleEventHandler handler, bool addHandler)
        {
            Bridge.SetConsoleCtrlHandler(handler, addHandler);
        }

        internal static void AllocateConsole()
        {
            Bridge.AllocConsole();

            var outputFilePointer = Bridge.CreateFile("CONOUT$", Native.Constants.GENERIC_READ | Native.Constants.GENERIC_WRITE, FileShare.Write, IntPtr.Zero, FileMode.OpenOrCreate, 0, IntPtr.Zero);

            Bridge.SetStdHandle(Native.Constants.STD_OUTPUT_HANDLE, outputFilePointer);

            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding) { AutoFlush = true });
        }

        /// <summary>
        /// Sets the computer awake state using the native Win32 SetThreadExecutionState API. This
        /// function is just a nice-to-have wrapper that helps avoid tracking the success or failure of
        /// the call.
        /// </summary>
        /// <param name="state">Single or multiple EXECUTION_STATE entries.</param>
        /// <returns>true if successful, false if failed</returns>
        private static bool SetAwakeState(ExecutionState state)
        {
            try
            {
                var stateResult = Bridge.SetThreadExecutionState(state);
                return stateResult != 0;
            }
            catch
            {
                return false;
            }
        }

        private static ExecutionState ComputeAwakeState(bool keepDisplayOn)
        {
            return keepDisplayOn
                ? ExecutionState.ES_SYSTEM_REQUIRED | ExecutionState.ES_DISPLAY_REQUIRED | ExecutionState.ES_CONTINUOUS
                : ExecutionState.ES_SYSTEM_REQUIRED | ExecutionState.ES_CONTINUOUS;
        }

        internal static void CancelExistingThread()
        {
            Logger.LogInfo($"Attempting to ensure that the thread is properly cleaned up...");

            // Resetting the thread state.
            _stateQueue.Add(ExecutionState.ES_CONTINUOUS);

            // Next, make sure that any existing background threads are terminated.
            _tokenSource.Cancel();
            _tokenSource.Dispose();

            _tokenSource = new CancellationTokenSource();
            Logger.LogInfo("Instantiating of new token source and thread token completed.");
        }

        internal static void SetIndefiniteKeepAwake(bool keepDisplayOn = false)
        {
            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AwakeIndefinitelyKeepAwakeEvent());

            CancelExistingThread();

            _stateQueue.Add(ComputeAwakeState(keepDisplayOn));

            TrayHelper.SetShellIcon(TrayHelper.HiddenWindowHandle, $"{Constants.FullAppName} [{Resources.AWAKE_TRAY_TEXT_INDEFINITE}]", _indefiniteIcon, TrayIconAction.Update);

            if (IsUsingPowerToysConfig)
            {
                try
                {
                    var currentSettings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();
                    var settingsChanged = currentSettings.Properties.Mode != AwakeMode.INDEFINITE ||
                                          currentSettings.Properties.KeepDisplayOn != keepDisplayOn;

                    if (settingsChanged)
                    {
                        currentSettings.Properties.Mode = AwakeMode.INDEFINITE;
                        currentSettings.Properties.KeepDisplayOn = keepDisplayOn;
                        ModuleSettings!.SaveSettings(JsonSerializer.Serialize(currentSettings), Constants.AppName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to handle indefinite keep awake command: {ex.Message}");
                }
            }
        }

        internal static void SetExpirableKeepAwake(DateTimeOffset expireAt, bool keepDisplayOn = true)
        {
            Logger.LogInfo($"Expirable keep-awake. Expected expiration date/time: {expireAt} with display on setting set to {keepDisplayOn}.");

            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AwakeExpirableKeepAwakeEvent());

            CancelExistingThread();

            if (expireAt > DateTimeOffset.Now)
            {
                Logger.LogInfo($"Starting expirable log for {expireAt}");
                _stateQueue.Add(ComputeAwakeState(keepDisplayOn));

                TrayHelper.SetShellIcon(TrayHelper.HiddenWindowHandle, $"{Constants.FullAppName} [{Resources.AWAKE_TRAY_TEXT_EXPIRATION} - {expireAt}]", _expirableIcon, TrayIconAction.Update);

                Observable.Timer(expireAt - DateTimeOffset.Now).Subscribe(
                _ =>
                {
                    Logger.LogInfo($"Completed expirable keep-awake.");
                    CancelExistingThread();

                    if (IsUsingPowerToysConfig)
                    {
                        SetPassiveKeepAwake();
                    }
                    else
                    {
                        Logger.LogInfo("Exiting after expirable keep awake.");
                        CompleteExit(Environment.ExitCode);
                    }
                },
                _tokenSource.Token);
            }
            else
            {
                Logger.LogError("The specified target date and time is not in the future.");
                Logger.LogError($"Current time: {DateTimeOffset.Now}\tTarget time: {expireAt}");
            }

            if (IsUsingPowerToysConfig)
            {
                try
                {
                    var currentSettings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();
                    var settingsChanged = currentSettings.Properties.Mode != AwakeMode.EXPIRABLE ||
                                          currentSettings.Properties.ExpirationDateTime != expireAt ||
                                          currentSettings.Properties.KeepDisplayOn != keepDisplayOn;

                    if (settingsChanged)
                    {
                        currentSettings.Properties.Mode = AwakeMode.EXPIRABLE;
                        currentSettings.Properties.KeepDisplayOn = keepDisplayOn;
                        currentSettings.Properties.ExpirationDateTime = expireAt;
                        ModuleSettings!.SaveSettings(JsonSerializer.Serialize(currentSettings), Constants.AppName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to handle indefinite keep awake command: {ex.Message}");
                }
            }
        }

        internal static void SetTimedKeepAwake(uint seconds, bool keepDisplayOn = true)
        {
            Logger.LogInfo($"Timed keep-awake. Expected runtime: {seconds} seconds with display on setting set to {keepDisplayOn}.");

            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AwakeTimedKeepAwakeEvent());

            CancelExistingThread();

            Logger.LogInfo($"Timed keep awake started for {seconds} seconds.");
            _stateQueue.Add(ComputeAwakeState(keepDisplayOn));

            TrayHelper.SetShellIcon(TrayHelper.HiddenWindowHandle, $"{Constants.FullAppName} [{Resources.AWAKE_TRAY_TEXT_TIMED}]", _timedIcon, TrayIconAction.Update);

            var timerObservable = Observable.Timer(TimeSpan.FromSeconds(seconds));
            var intervalObservable = Observable.Interval(TimeSpan.FromSeconds(1)).TakeUntil(timerObservable);

            var combinedObservable = Observable.CombineLatest(intervalObservable, timerObservable.StartWith(0), (elapsedSeconds, _) => elapsedSeconds + 1);

            combinedObservable.Subscribe(
                elapsedSeconds =>
                {
                    var timeRemaining = seconds - (uint)elapsedSeconds;
                    if (timeRemaining >= 0)
                    {
                        TrayHelper.SetShellIcon(TrayHelper.HiddenWindowHandle, $"{Constants.FullAppName} [{Resources.AWAKE_TRAY_TEXT_TIMED}]\n{TimeSpan.FromSeconds(timeRemaining).ToHumanReadableString()}", _timedIcon, TrayIconAction.Update);
                    }
                },
                () =>
                {
                    Console.WriteLine("Completed timed thread.");
                    CancelExistingThread();

                    if (IsUsingPowerToysConfig)
                    {
                        // If we're using PowerToys settings, we need to make sure that
                        // we just switch over the Passive Keep-Awake.
                        SetPassiveKeepAwake();
                    }
                    else
                    {
                        Logger.LogInfo("Exiting after timed keep-awake.");
                        CompleteExit(Environment.ExitCode);
                    }
                },
                _tokenSource.Token);

            if (IsUsingPowerToysConfig)
            {
                try
                {
                    var currentSettings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();
                    var timeSpan = TimeSpan.FromSeconds(seconds);
                    var settingsChanged = currentSettings.Properties.Mode != AwakeMode.TIMED ||
                                          currentSettings.Properties.IntervalHours != (uint)timeSpan.Hours ||
                                          currentSettings.Properties.IntervalMinutes != (uint)timeSpan.Minutes;

                    if (settingsChanged)
                    {
                        currentSettings.Properties.Mode = AwakeMode.TIMED;
                        currentSettings.Properties.IntervalHours = (uint)timeSpan.Hours;
                        currentSettings.Properties.IntervalMinutes = (uint)timeSpan.Minutes;
                        ModuleSettings!.SaveSettings(JsonSerializer.Serialize(currentSettings), Constants.AppName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to handle timed keep awake command: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Performs a clean exit from Awake.
        /// </summary>
        /// <param name="exitCode">Exit code to exit with.</param>
        internal static void CompleteExit(int exitCode)
        {
            SetPassiveKeepAwake(updateSettings: false);

            if (TrayHelper.HiddenWindowHandle != IntPtr.Zero)
            {
                // Delete the icon.
                TrayHelper.SetShellIcon(TrayHelper.HiddenWindowHandle, string.Empty, null, TrayIconAction.Delete);

                // Close the message window that we used for the tray.
                Bridge.SendMessage(TrayHelper.HiddenWindowHandle, Native.Constants.WM_CLOSE, 0, 0);

                Bridge.DestroyWindow(TrayHelper.HiddenWindowHandle);
            }

            Bridge.PostQuitMessage(exitCode);
            Environment.Exit(exitCode);
        }

        /// <summary>
        /// Gets the operating system for logging purposes.
        /// </summary>
        /// <returns>Returns the string representing the current OS build.</returns>
        internal static string GetOperatingSystemBuild()
        {
            try
            {
                RegistryKey? registryKey = Registry.LocalMachine.OpenSubKey(Constants.BuildRegistryLocation);

                if (registryKey != null)
                {
                    var versionString = $"{registryKey.GetValue("ProductName")} {registryKey.GetValue("DisplayVersion")} {registryKey.GetValue("BuildLabEx")}";
                    return versionString;
                }
                else
                {
                    Logger.LogError("Registry key acquisition for OS failed.");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Could not get registry key for the build number. Error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Generates the default system tray options in situations where no custom options are provided.
        /// </summary>
        /// <returns>Returns a dictionary of default Awake timed interval options.</returns>
        internal static Dictionary<string, int> GetDefaultTrayOptions()
        {
            Dictionary<string, int> optionsList = new()
            {
                { string.Format(CultureInfo.InvariantCulture, AwakeMinutes, 30), 1800 },
                { string.Format(CultureInfo.InvariantCulture, AwakeHours, 1), 3600 },
                { string.Format(CultureInfo.InvariantCulture, AwakeHours, 2), 7200 },
            };
            return optionsList;
        }

        /// <summary>
        /// Resets the computer to standard power settings.
        /// </summary>
        /// <param name="updateSettings">In certain cases, such as exits, we want to make sure that settings are not reset for the passive mode but rather retained based on previous execution. Default is to save settings, but otherwise it can be overridden.</param>
        internal static void SetPassiveKeepAwake(bool updateSettings = true)
        {
            Logger.LogInfo($"Operating in passive mode (computer's standard power plan). No custom keep awake settings enabled.");

            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AwakeNoKeepAwakeEvent());

            CancelExistingThread();

            TrayHelper.SetShellIcon(TrayHelper.HiddenWindowHandle, $"{Constants.FullAppName} [{Resources.AWAKE_TRAY_TEXT_OFF}]", _disabledIcon, TrayIconAction.Update);

            if (IsUsingPowerToysConfig && updateSettings)
            {
                try
                {
                    var currentSettings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();

                    if (currentSettings.Properties.Mode != AwakeMode.PASSIVE)
                    {
                        currentSettings.Properties.Mode = AwakeMode.PASSIVE;
                        ModuleSettings!.SaveSettings(JsonSerializer.Serialize(currentSettings), Constants.AppName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to reset Awake mode: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Sets the display settings.
        /// </summary>
        internal static void SetDisplay()
        {
            if (IsUsingPowerToysConfig)
            {
                try
                {
                    var currentSettings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();
                    currentSettings.Properties.KeepDisplayOn = !currentSettings.Properties.KeepDisplayOn;
                    ModuleSettings!.SaveSettings(JsonSerializer.Serialize(currentSettings), Constants.AppName);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to handle display setting command: {ex.Message}");
                }
            }
        }
    }
}
