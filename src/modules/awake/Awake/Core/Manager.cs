// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    public delegate bool ConsoleEventHandler(ControlType ctrlType);

    /// <summary>
    /// Helper class that allows talking to Win32 APIs without having to rely on PInvoke in other parts
    /// of the codebase.
    /// </summary>
    public class Manager
    {
        internal static bool IsUsingPowerToysConfig { get; set; }

        internal static SettingsUtils? ModuleSettings { get; set; }

        private static AwakeMode CurrentOperatingMode { get; set; }

        private static bool IsDisplayOn { get; set; }

        private static uint TimeRemaining { get; set; }

        private static string ScreenStateString => IsDisplayOn ? Resources.AWAKE_SCREEN_ON : Resources.AWAKE_SCREEN_OFF;

        private static int ProcessId { get; set; }

        private static DateTimeOffset ExpireAt { get; set; }

        private static readonly CompositeFormat AwakeMinute = CompositeFormat.Parse(Resources.AWAKE_MINUTE);
        private static readonly CompositeFormat AwakeMinutes = CompositeFormat.Parse(Resources.AWAKE_MINUTES);
        private static readonly CompositeFormat AwakeHour = CompositeFormat.Parse(Resources.AWAKE_HOUR);
        private static readonly CompositeFormat AwakeHours = CompositeFormat.Parse(Resources.AWAKE_HOURS);
        private static readonly BlockingCollection<ExecutionState> _stateQueue;
        private static CancellationTokenSource _tokenSource;

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
                Thread.CurrentThread.IsBackground = false;
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

            nint outputFilePointer = Bridge.CreateFile("CONOUT$", Native.Constants.GENERIC_READ | Native.Constants.GENERIC_WRITE, FileShare.Write, IntPtr.Zero, FileMode.OpenOrCreate, 0, IntPtr.Zero);

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
                ExecutionState stateResult = Bridge.SetThreadExecutionState(state);
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
            Logger.LogInfo("Ensuring the thread is properly cleaned up...");

            // Reset the thread state and handle cancellation.
            _stateQueue.Add(ExecutionState.ES_CONTINUOUS);

            if (_tokenSource != null)
            {
                _tokenSource.Cancel();
                _tokenSource.Dispose();
            }
            else
            {
                Logger.LogWarning("Token source is null.");
            }

            _tokenSource = new CancellationTokenSource();

            Logger.LogInfo("New token source and thread token instantiated.");
        }

        internal static void SetModeShellIcon(bool forceAdd = false)
        {
            string iconText = string.Empty;
            Icon? icon = null;

            switch (CurrentOperatingMode)
            {
                case AwakeMode.INDEFINITE:
                    string processText = ProcessId == 0
                        ? string.Empty
                        : $" - {Resources.AWAKE_TRAY_TEXT_PID_BINDING}: {ProcessId}";
                    iconText = $"{Constants.FullAppName} [{Resources.AWAKE_TRAY_TEXT_INDEFINITE}{processText}][{ScreenStateString}]";
                    icon = TrayHelper.IndefiniteIcon;
                    break;

                case AwakeMode.PASSIVE:
                    iconText = $"{Constants.FullAppName} [{Resources.AWAKE_TRAY_TEXT_OFF}]";
                    icon = TrayHelper.DisabledIcon;
                    break;

                case AwakeMode.EXPIRABLE:
                    iconText = $"{Constants.FullAppName} [{Resources.AWAKE_TRAY_TEXT_EXPIRATION}][{ScreenStateString}][{ExpireAt:yyyy-MM-dd HH:mm:ss}]";
                    icon = TrayHelper.ExpirableIcon;
                    break;

                case AwakeMode.TIMED:
                    iconText = $"{Constants.FullAppName} [{Resources.AWAKE_TRAY_TEXT_TIMED}][{ScreenStateString}]";
                    icon = TrayHelper.TimedIcon;
                    break;
            }

            TrayHelper.SetShellIcon(
                TrayHelper.WindowHandle,
                iconText,
                icon,
                forceAdd ? TrayIconAction.Add : TrayIconAction.Update);
        }

        internal static void SetIndefiniteKeepAwake(bool keepDisplayOn = false, int processId = 0, [CallerMemberName] string callerName = "")
        {
            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AwakeIndefinitelyKeepAwakeEvent());

            CancelExistingThread();

            if (IsUsingPowerToysConfig)
            {
                try
                {
                    AwakeSettings currentSettings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();
                    bool settingsChanged = currentSettings.Properties.Mode != AwakeMode.INDEFINITE ||
                                          currentSettings.Properties.KeepDisplayOn != keepDisplayOn;

                    if (settingsChanged)
                    {
                        currentSettings.Properties.Mode = AwakeMode.INDEFINITE;
                        currentSettings.Properties.KeepDisplayOn = keepDisplayOn;
                        ModuleSettings!.SaveSettings(JsonSerializer.Serialize(currentSettings), Constants.AppName);

                        // We return here because when the settings are saved, they will be automatically
                        // processed. That means that when they are processed, the indefinite keep-awake will kick-in properly
                        // and we avoid double execution.
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to handle indefinite keep awake command invoked by {callerName}: {ex.Message}");
                }
            }

            Logger.LogInfo($"Indefinite keep-awake starting, invoked by {callerName}...");

            _stateQueue.Add(ComputeAwakeState(keepDisplayOn));

            IsDisplayOn = keepDisplayOn;
            CurrentOperatingMode = AwakeMode.INDEFINITE;
            ProcessId = processId;

            SetModeShellIcon();
        }

        internal static void SetExpirableKeepAwake(DateTimeOffset expireAt, bool keepDisplayOn = true, [CallerMemberName] string callerName = "")
        {
            Logger.LogInfo($"Expirable keep-awake invoked by {callerName}. Expected expiration date/time: {expireAt} with display on setting set to {keepDisplayOn}.");
            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AwakeExpirableKeepAwakeEvent());

            CancelExistingThread();

            if (IsUsingPowerToysConfig)
            {
                try
                {
                    AwakeSettings currentSettings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();
                    bool settingsChanged = currentSettings.Properties.Mode != AwakeMode.EXPIRABLE ||
                                          currentSettings.Properties.ExpirationDateTime != expireAt ||
                                          currentSettings.Properties.KeepDisplayOn != keepDisplayOn;

                    if (settingsChanged)
                    {
                        currentSettings.Properties.Mode = AwakeMode.EXPIRABLE;
                        currentSettings.Properties.KeepDisplayOn = keepDisplayOn;
                        currentSettings.Properties.ExpirationDateTime = expireAt;
                        ModuleSettings!.SaveSettings(JsonSerializer.Serialize(currentSettings), Constants.AppName);

                        // We return here because when the settings are saved, they will be automatically
                        // processed. That means that when they are processed, the expirable keep-awake will kick-in properly
                        // and we avoid double execution.
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to handle indefinite keep awake command: {ex.Message}");
                }
            }

            Logger.LogInfo($"Expirable keep-awake starting...");

            if (expireAt <= DateTimeOffset.Now)
            {
                Logger.LogError($"The specified target date and time is not in the future. Current time: {DateTimeOffset.Now}, Target time: {expireAt}");
                return;
            }

            Logger.LogInfo($"Starting expirable log for {expireAt}");
            _stateQueue.Add(ComputeAwakeState(keepDisplayOn));

            IsDisplayOn = keepDisplayOn;
            CurrentOperatingMode = AwakeMode.EXPIRABLE;
            ExpireAt = expireAt;

            SetModeShellIcon();

            TimeSpan remainingTime = expireAt - DateTimeOffset.Now;

            Observable.Timer(remainingTime).Subscribe(
                _ => HandleTimerCompletion("expirable"),
                _tokenSource.Token);
        }

        internal static void SetTimedKeepAwake(uint seconds, bool keepDisplayOn = true, [CallerMemberName] string callerName = "")
        {
            Logger.LogInfo($"Timed keep-awake invoked by {callerName}. Expected runtime: {seconds} seconds with display on setting set to {keepDisplayOn}.");
            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AwakeTimedKeepAwakeEvent());

            CancelExistingThread();

            if (IsUsingPowerToysConfig)
            {
                try
                {
                    AwakeSettings currentSettings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();
                    TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);

                    uint totalHours = (uint)timeSpan.TotalHours;
                    uint remainingMinutes = (uint)Math.Ceiling(timeSpan.TotalMinutes % 60);

                    bool settingsChanged = currentSettings.Properties.Mode != AwakeMode.TIMED ||
                                          currentSettings.Properties.IntervalHours != totalHours ||
                                          currentSettings.Properties.IntervalMinutes != remainingMinutes;

                    if (settingsChanged)
                    {
                        currentSettings.Properties.Mode = AwakeMode.TIMED;
                        currentSettings.Properties.IntervalHours = totalHours;
                        currentSettings.Properties.IntervalMinutes = remainingMinutes;
                        ModuleSettings!.SaveSettings(JsonSerializer.Serialize(currentSettings), Constants.AppName);

                        // We return here because when the settings are saved, they will be automatically
                        // processed. That means that when they are processed, the timed keep-awake will kick-in properly
                        // and we avoid double execution.
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to handle timed keep awake command: {ex.Message}");
                }
            }

            Logger.LogInfo($"Timed keep-awake starting...");

            _stateQueue.Add(ComputeAwakeState(keepDisplayOn));

            IsDisplayOn = keepDisplayOn;
            CurrentOperatingMode = AwakeMode.TIMED;

            SetModeShellIcon();

            var targetExpiryTime = DateTimeOffset.Now.AddSeconds(seconds);

            Observable.Interval(TimeSpan.FromSeconds(1))
                .Select(_ => targetExpiryTime - DateTimeOffset.Now)
                .TakeWhile(remaining => remaining.TotalSeconds > 0)
                .Subscribe(
                    remainingTimeSpan =>
                    {
                        TimeRemaining = (uint)remainingTimeSpan.TotalSeconds;

                        TrayHelper.SetShellIcon(
                            TrayHelper.WindowHandle,
                            $"{Constants.FullAppName} [{Resources.AWAKE_TRAY_TEXT_TIMED}][{ScreenStateString}][{remainingTimeSpan.ToHumanReadableString()}]",
                            TrayHelper.TimedIcon,
                            TrayIconAction.Update);
                    },
                    _ => HandleTimerCompletion("timed"),
                    _tokenSource.Token);
        }

        /// <summary>
        /// Handles the common logic that should execute when a keep-awake timer completes. Resets
        /// the application state to Passive if configured; otherwise it exits.
        /// </summary>
        private static void HandleTimerCompletion(string timerType)
        {
            Logger.LogInfo($"Completed {timerType} keep-awake.");
            CancelExistingThread();

            if (IsUsingPowerToysConfig)
            {
                // If running under PowerToys settings, just revert to the default Passive state.
                SetPassiveKeepAwake();
            }
            else
            {
                // If running as a standalone process, exit cleanly.
                Logger.LogInfo($"Exiting after {timerType} keep-awake.");
                CompleteExit(Environment.ExitCode);
            }
        }

        /// <summary>
        /// Performs a clean exit from Awake.
        /// </summary>
        /// <param name="exitCode">Exit code to exit with.</param>
        internal static void CompleteExit(int exitCode)
        {
            SetPassiveKeepAwake(updateSettings: false);

            if (TrayHelper.WindowHandle != IntPtr.Zero)
            {
                // Delete the icon.
                TrayHelper.SetShellIcon(TrayHelper.WindowHandle, string.Empty, null, TrayIconAction.Delete);

                // Close the message window that we used for the tray.
                Bridge.SendMessage(TrayHelper.WindowHandle, Native.Constants.WM_CLOSE, 0, 0);

                Bridge.DestroyWindow(TrayHelper.WindowHandle);
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
                    string versionString = $"{registryKey.GetValue("ProductName")} {registryKey.GetValue("DisplayVersion")} {registryKey.GetValue("BuildLabEx")}";
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
        internal static Dictionary<string, uint> GetDefaultTrayOptions()
        {
            Dictionary<string, uint> optionsList = new()
            {
                { string.Format(CultureInfo.InvariantCulture, AwakeMinutes, 30), 1800 },
                { string.Format(CultureInfo.InvariantCulture, AwakeHour, 1), 3600 },
                { string.Format(CultureInfo.InvariantCulture, AwakeHours, 2), 7200 },
            };
            return optionsList;
        }

        /// <summary>
        /// Resets the computer to standard power settings.
        /// </summary>
        /// <param name="updateSettings">In certain cases, such as exits, we want to make sure that settings are not reset for the passive mode but rather retained based on previous execution. Default is to save settings, but otherwise it can be overridden.</param>
        internal static void SetPassiveKeepAwake(bool updateSettings = true, [CallerMemberName] string callerName = "")
        {
            Logger.LogInfo($"Operating in passive mode (computer's standard power plan). Invoked by {callerName}. No custom keep awake settings enabled.");
            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AwakeNoKeepAwakeEvent());

            CancelExistingThread();

            if (IsUsingPowerToysConfig && updateSettings)
            {
                try
                {
                    AwakeSettings currentSettings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();

                    if (currentSettings.Properties.Mode != AwakeMode.PASSIVE)
                    {
                        currentSettings.Properties.Mode = AwakeMode.PASSIVE;
                        ModuleSettings!.SaveSettings(JsonSerializer.Serialize(currentSettings), Constants.AppName);

                        // We return here because when the settings are saved, they will be automatically
                        // processed. That means that when they are processed, the passive keep-awake will kick-in properly
                        // and we avoid double execution.
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to reset Awake mode: {ex.Message}");
                }
            }

            Logger.LogInfo($"Passive keep-awake starting...");

            CurrentOperatingMode = AwakeMode.PASSIVE;

            SetModeShellIcon();
        }

        /// <summary>
        /// Sets the display settings.
        /// </summary>
        internal static void SetDisplay([CallerMemberName] string callerName = "")
        {
            Logger.LogInfo($"Setting display configuration from settings. Invoked by {callerName}.");
            if (IsUsingPowerToysConfig)
            {
                try
                {
                    AwakeSettings currentSettings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();
                    currentSettings.Properties.KeepDisplayOn = !currentSettings.Properties.KeepDisplayOn;

                    // We want to make sure that if the display setting changes (e.g., through the tray)
                    // then we do not reset the counter from zero. Because the settings are only storing
                    // hours and minutes, we round up the minutes value up when changes occur.
                    if (CurrentOperatingMode == AwakeMode.TIMED && TimeRemaining > 0)
                    {
                        TimeSpan timeSpan = TimeSpan.FromSeconds(TimeRemaining);

                        currentSettings.Properties.IntervalHours = (uint)timeSpan.TotalHours;
                        currentSettings.Properties.IntervalMinutes = (uint)Math.Ceiling(timeSpan.TotalMinutes % 60);
                    }

                    ModuleSettings!.SaveSettings(JsonSerializer.Serialize(currentSettings), Constants.AppName);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to handle display setting command: {ex.Message}");
                }
            }
        }

        public static Process? GetParentProcess()
        {
            return GetParentProcess(Process.GetCurrentProcess().Handle);
        }

        private static Process? GetParentProcess(IntPtr handle)
        {
            ProcessBasicInformation pbi = default;
            int status = Bridge.NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf<ProcessBasicInformation>(), out _);

            return status != 0 ? null : Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
        }
    }
}
