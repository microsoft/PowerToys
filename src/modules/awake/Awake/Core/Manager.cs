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
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using Awake.Core.Models;
using Awake.Core.Native;

// New usage tracking namespace
using Awake.Core.Usage;
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

        // Foreground usage tracker instance (lifecycle managed by Program)
        internal static ForegroundUsageTracker? UsageTracker { get; set; }

        private static PowerSchemeManager powerSchemeManager;

        // Power scheme auto-switch (activity mode)
        private static string? _originalPowerSchemeGuid;
        private static bool _powerSchemeSwitched;

        // Process monitoring fields
        private static List<string> _processMonitoringList = [];
        private static bool _processMonitoringActive;
        private static IDisposable? _processMonitoringSubscription;
        private static uint _processCheckInterval;
        private static bool _processKeepDisplay;

        static Manager()
        {
            _tokenSource = new CancellationTokenSource();
            powerSchemeManager = new PowerSchemeManager();
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

        // Activity mode state
        private static bool _activityActive;
        private static DateTimeOffset _activityLastHigh;
        private static uint _activityCpu;
        private static uint _activityMem;
        private static uint _activityNetKBps;
        private static uint _activitySample;
        private static uint _activityTimeout;
        private static bool _activityKeepDisplay;
        private static PerformanceCounter? _cpuCounter;
        private static PerformanceCounter? _memCounter;
        private static List<PerformanceCounter>? _netCounters;

        internal static void CancelExistingThread()
        {
            Logger.LogInfo("Ensuring the thread is properly cleaned up...");

            // Reset the thread state and handle cancellation.
            _stateQueue.Add(ExecutionState.ES_CONTINUOUS);

            // Clean up process monitoring subscription if active
            _processMonitoringSubscription?.Dispose();
            _processMonitoringSubscription = null;

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
                case AwakeMode.ACTIVITY:
                    iconText = $"{Constants.FullAppName} [{Resources.AWAKE_TRAY_TEXT_ACTIVITY}][{ScreenStateString}]";
                    icon = TrayHelper.IndefiniteIcon; // Placeholder icon
                    break;
                case AwakeMode.PROCESS:
                    string processesText = _processMonitoringList.Count > 0 ? string.Join(", ", _processMonitoringList) : "None";
                    iconText = $"{Constants.FullAppName} [Process Monitor][{ScreenStateString}][{processesText}]";
                    icon = TrayHelper.IndefiniteIcon; // Use same icon as indefinite for now
                    break;
            }

            TrayHelper.SetShellIcon(
                TrayHelper.WindowHandle,
                iconText,
                icon,
                forceAdd ? TrayIconAction.Add : TrayIconAction.Update);
        }

        private static void CaptureOriginalPowerScheme()
        {
            try
            {
                powerSchemeManager.RefreshSchemes();
                _originalPowerSchemeGuid = powerSchemeManager
                    .GetAllSchemes()
                    .FirstOrDefault(s => s.IsActive)?.PSGuid;
                Logger.LogInfo($"Captured original power scheme: {_originalPowerSchemeGuid ?? "UNKNOWN"}");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to capture original power scheme: {ex.Message}");
            }
        }

        private static void SwitchToHighestPerformanceIfNeeded()
        {
            if (_powerSchemeSwitched)
            {
                return;
            }

            try
            {
                powerSchemeManager.RefreshSchemes();
                var highest = powerSchemeManager.GetHighestPerformanceScheme();
                if (highest == null)
                {
                    Logger.LogInfo("No power schemes found when attempting high performance switch.");
                    return;
                }

                if (highest.IsActive)
                {
                    Logger.LogInfo("Already on highest performance scheme – no switch needed.");
                    return;
                }

                if (_originalPowerSchemeGuid == null)
                {
                    CaptureOriginalPowerScheme();
                }

                if (powerSchemeManager.SwitchScheme(highest.PSGuid))
                {
                    _powerSchemeSwitched = true;
                    Logger.LogInfo($"Switched to highest performance scheme: {highest.Name} ({highest.PSGuid})");
                }
                else
                {
                    Logger.LogWarning($"Failed to switch to highest performance scheme: {highest.Name} ({highest.PSGuid})");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Exception while attempting to switch power scheme: {ex.Message}");
            }
        }

        private static void RestoreOriginalPowerSchemeIfNeeded()
        {
            if (!_powerSchemeSwitched || string.IsNullOrWhiteSpace(_originalPowerSchemeGuid))
            {
                return;
            }

            try
            {
                if (powerSchemeManager.SwitchScheme(_originalPowerSchemeGuid))
                {
                    Logger.LogInfo($"Restored original power scheme: {_originalPowerSchemeGuid}");
                }
                else
                {
                    Logger.LogWarning($"Failed to restore original power scheme: {_originalPowerSchemeGuid}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Exception restoring original power scheme: {ex.Message}");
            }
            finally
            {
                _powerSchemeSwitched = false;
            }
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
                _ =>
                {
                    Logger.LogInfo("Completed expirable keep-awake.");
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

            ulong desiredDuration = (ulong)seconds * 1000;
            ulong targetDuration = Math.Min(desiredDuration, uint.MaxValue - 1) / 1000;

            if (desiredDuration > uint.MaxValue)
            {
                Logger.LogInfo($"The desired interval of {seconds} seconds ({desiredDuration}ms) exceeds the limit. Defaulting to maximum possible value: {targetDuration} seconds. Read more about existing limits in the official documentation: https://aka.ms/powertoys/awake");
            }

            IObservable<long> timerObservable = Observable.Timer(TimeSpan.FromSeconds(targetDuration));
            IObservable<long> intervalObservable = Observable.Interval(TimeSpan.FromSeconds(1)).TakeUntil(timerObservable);
            IObservable<long> combinedObservable = Observable.CombineLatest(intervalObservable, timerObservable.StartWith(0), (elapsedSeconds, _) => elapsedSeconds + 1);

            combinedObservable.Subscribe(
                elapsedSeconds =>
                {
                    TimeRemaining = (uint)targetDuration - (uint)elapsedSeconds;
                    if (TimeRemaining >= 0)
                    {
                        TrayHelper.SetShellIcon(
                            TrayHelper.WindowHandle,
                            $"{Constants.FullAppName} [{Resources.AWAKE_TRAY_TEXT_TIMED}][{ScreenStateString}][{TimeSpan.FromSeconds(TimeRemaining).ToHumanReadableString()}]",
                            TrayHelper.TimedIcon,
                            TrayIconAction.Update);
                    }
                },
                () =>
                {
                    Logger.LogInfo("Completed timed thread.");
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
        }

        /// <summary>
        /// Performs a clean exit from Awake.
        /// </summary>
        /// <param name="exitCode">Exit code to exit with.</param>
        internal static void CompleteExit(int exitCode)
        {
            SetPassiveKeepAwake(updateSettings: false);
            RestoreOriginalPowerSchemeIfNeeded();
            if (TrayHelper.WindowHandle != IntPtr.Zero)
            {
                TrayHelper.SetShellIcon(TrayHelper.WindowHandle, string.Empty, null, TrayIconAction.Delete);
                Bridge.SendMessage(TrayHelper.WindowHandle, Native.Constants.WM_CLOSE, 0, 0);
                Bridge.DestroyWindow(TrayHelper.WindowHandle);
            }

            // Dispose usage tracker (flushes data)
            try
            {
                UsageTracker?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed disposing UsageTracker: {ex.Message}");
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

        internal static void SetActivityBasedKeepAwake(
            uint cpuThresholdPercent,
            uint memThresholdPercent,
            uint netThresholdKBps,
            uint sampleIntervalSeconds,
            uint inactivityTimeoutSeconds,
            bool keepDisplayOn,
            [CallerMemberName] string callerName = "")
        {
            Logger.LogInfo($"Activity-based keep-awake invoked by {callerName}. CPU>={cpuThresholdPercent}%, MEM>={memThresholdPercent}%, NET>={netThresholdKBps}KB/s sample={sampleIntervalSeconds}s timeout={inactivityTimeoutSeconds}s display={keepDisplayOn}.");

            CancelExistingThread();

            if (IsUsingPowerToysConfig)
            {
                try
                {
                    AwakeSettings currentSettings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();
                    bool settingsChanged =
                        currentSettings.Properties.Mode != AwakeMode.ACTIVITY ||
                        currentSettings.Properties.ActivityCpuThresholdPercent != cpuThresholdPercent ||
                        currentSettings.Properties.ActivityMemoryThresholdPercent != memThresholdPercent ||
                        currentSettings.Properties.ActivityNetworkThresholdKBps != netThresholdKBps ||
                        currentSettings.Properties.ActivitySampleIntervalSeconds != sampleIntervalSeconds ||
                        currentSettings.Properties.ActivityInactivityTimeoutSeconds != inactivityTimeoutSeconds ||
                        currentSettings.Properties.KeepDisplayOn != keepDisplayOn;

                    if (settingsChanged)
                    {
                        currentSettings.Properties.Mode = AwakeMode.ACTIVITY;
                        currentSettings.Properties.ActivityCpuThresholdPercent = cpuThresholdPercent;
                        currentSettings.Properties.ActivityMemoryThresholdPercent = memThresholdPercent;
                        currentSettings.Properties.ActivityNetworkThresholdKBps = netThresholdKBps;
                        currentSettings.Properties.ActivitySampleIntervalSeconds = sampleIntervalSeconds;
                        currentSettings.Properties.ActivityInactivityTimeoutSeconds = inactivityTimeoutSeconds;
                        currentSettings.Properties.KeepDisplayOn = keepDisplayOn;
                        ModuleSettings!.SaveSettings(JsonSerializer.Serialize(currentSettings), Constants.AppName);
                        return; // Settings will be processed triggering this again
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to persist activity mode settings: {ex.Message}");
                }
            }

            // Initialize performance counters
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                _netCounters = PerformanceCounterCategory.GetCategories()
                    .FirstOrDefault(c => c.CategoryName == "Network Interface")?
                    .GetInstanceNames()
                    .Select(n => new PerformanceCounter("Network Interface", "Bytes Total/sec", n))
                    .ToList() ?? new List<PerformanceCounter>();
                _cpuCounter.NextValue(); // Prime CPU counter
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize performance counters for activity mode: {ex.Message}");
                return;
            }

            _activityCpu = cpuThresholdPercent;
            _activityMem = memThresholdPercent;
            _activityNetKBps = netThresholdKBps;
            _activitySample = Math.Max(1, sampleIntervalSeconds);
            _activityTimeout = Math.Max(5, inactivityTimeoutSeconds);
            _activityKeepDisplay = keepDisplayOn;
            _activityLastHigh = DateTimeOffset.Now;
            _activityActive = true;

            CurrentOperatingMode = AwakeMode.ACTIVITY;
            IsDisplayOn = keepDisplayOn;
            SetModeShellIcon();

            // Capture original scheme before any switch
            CaptureOriginalPowerScheme();

            TimeSpan sampleInterval = TimeSpan.FromSeconds(_activitySample);

            Observable.Interval(sampleInterval).Subscribe(
                _ =>
                {
                    if (!_activityActive)
                    {
                        return;
                    }

                    float cpu = 0;
                    float mem = 0;
                    double netKBps = 0;

                    try
                    {
                        cpu = _cpuCounter?.NextValue() ?? 0;
                        mem = _memCounter?.NextValue() ?? 0;
                        if (_netCounters != null && _netCounters.Count > 0)
                        {
                            netKBps = _netCounters.Sum(c => c.NextValue()) / 1024.0;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Performance counter read failure: {ex.Message}");
                    }

                    bool above =
                        (_activityCpu == 0 || cpu >= _activityCpu) ||
                        (_activityMem == 0 || mem >= _activityMem) ||
                        (_activityNetKBps == 0 || netKBps >= _activityNetKBps);

                    if (above)
                    {
                        _activityLastHigh = DateTimeOffset.Now;
                        _stateQueue.Add(ComputeAwakeState(_activityKeepDisplay));
                        SwitchToHighestPerformanceIfNeeded();
                    }

                    TrayHelper.SetShellIcon(
                        TrayHelper.WindowHandle,
                        $"{Constants.FullAppName} [Activity][{ScreenStateString}][CPU {cpu:0.#}% | MEM {mem:0.#}% | NET {netKBps:0.#}KB/s]",
                        TrayHelper.IndefiniteIcon,
                        TrayIconAction.Update);

                    if ((DateTimeOffset.Now - _activityLastHigh).TotalSeconds >= _activityTimeout)
                    {
                        Logger.LogInfo("Activity thresholds not met within timeout window. Ending activity mode.");
                        _activityActive = false;
                        CancelExistingThread();
                        RestoreOriginalPowerSchemeIfNeeded();

                        if (IsUsingPowerToysConfig)
                        {
                            SetPassiveKeepAwake();
                        }
                        else
                        {
                            CompleteExit(Environment.ExitCode);
                        }
                    }
                },
                ex =>
                {
                    Logger.LogError($"Activity mode observable failure: {ex.Message}");
                },
                _tokenSource.Token);
        }

        internal static void SetProcessBasedKeepAwake(
            List<string> processNames,
            uint checkIntervalSeconds,
            bool keepDisplayOn,
            [CallerMemberName] string callerName = "")
        {
            Logger.LogInfo($"Process-based keep-awake invoked by {callerName}. Processes: [{string.Join(", ", processNames)}], CheckInterval: {checkIntervalSeconds}s, Display: {keepDisplayOn}.");

            CancelExistingThread();

            if (IsUsingPowerToysConfig)
            {
                try
                {
                    AwakeSettings currentSettings = ModuleSettings!.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();
                    bool settingsChanged =
                        currentSettings.Properties.Mode != AwakeMode.PROCESS ||
                        !currentSettings.Properties.ProcessMonitoringList.SequenceEqual(processNames) ||
                        currentSettings.Properties.ProcessCheckIntervalSeconds != checkIntervalSeconds ||
                        currentSettings.Properties.KeepDisplayOn != keepDisplayOn;

                    if (settingsChanged)
                    {
                        currentSettings.Properties.Mode = AwakeMode.PROCESS;
                        currentSettings.Properties.ProcessMonitoringList = new List<string>(processNames);
                        currentSettings.Properties.ProcessCheckIntervalSeconds = checkIntervalSeconds;
                        currentSettings.Properties.KeepDisplayOn = keepDisplayOn;
                        ModuleSettings!.SaveSettings(JsonSerializer.Serialize(currentSettings), Constants.AppName);
                        return; // Settings will be processed triggering this again
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to persist process monitoring settings: {ex.Message}");
                }
            }

            _processMonitoringList = [.. processNames];
            _processCheckInterval = Math.Max(1, checkIntervalSeconds);
            _processKeepDisplay = keepDisplayOn;
            _processMonitoringActive = true;

            CurrentOperatingMode = AwakeMode.PROCESS;
            IsDisplayOn = keepDisplayOn;
            SetModeShellIcon();

            TimeSpan checkInterval = TimeSpan.FromSeconds(_processCheckInterval);

            Observable.Interval(checkInterval).Subscribe(
                _ =>
                {
                    if (!_processMonitoringActive)
                    {
                        return;
                    }

                    bool anyTargetProcessRunning = false;
                    List<string> runningProcesses = new();

                    try
                    {
                        foreach (string processName in _processMonitoringList)
                        {
                            if (string.IsNullOrWhiteSpace(processName))
                            {
                                continue;
                            }

                            // Remove .exe extension if present for process name comparison
                            string processNameWithoutExt = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                                ? processName.Substring(0, processName.Length - 4)
                                : processName;

                            Process[] processes = Process.GetProcessesByName(processNameWithoutExt);
                            if (processes.Length > 0)
                            {
                                anyTargetProcessRunning = true;
                                runningProcesses.Add(processNameWithoutExt);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Process monitoring check failure: {ex.Message}");
                    }

                    if (anyTargetProcessRunning)
                    {
                        _stateQueue.Add(ComputeAwakeState(_processKeepDisplay));

                        TrayHelper.SetShellIcon(
                            TrayHelper.WindowHandle,
                            $"{Constants.FullAppName} [Process Monitor][{ScreenStateString}][Running: {string.Join(", ", runningProcesses)}]",
                            TrayHelper.IndefiniteIcon,
                            TrayIconAction.Update);
                    }
                    else
                    {
                        Logger.LogInfo("No target processes running. Ending process monitoring mode.");
                        _processMonitoringActive = false;
                        CancelExistingThread();

                        if (IsUsingPowerToysConfig)
                        {
                            SetPassiveKeepAwake();
                        }
                        else
                        {
                            CompleteExit(Environment.ExitCode);
                        }
                    }
                },
                ex =>
                {
                    Logger.LogError($"Process monitoring observable failure: {ex.Message}");
                },
                _tokenSource.Token);
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

        /// <summary>
        /// Gets the current Awake configuration status.
        /// </summary>
        /// <returns>Current configuration status information.</returns>
        public static object GetCurrentConfig()
        {
            try
            {
                AwakeSettings currentSettings = ModuleSettings?.GetSettings<AwakeSettings>(Constants.AppName) ?? new AwakeSettings();

                var baseConfig = new
                {
                    mode = CurrentOperatingMode.ToString(),
                    keepDisplayOn = IsDisplayOn,
                    processId = ProcessId,
                    isUsingPowerToysConfig = IsUsingPowerToysConfig,
                };

                // Return different parameters based on the current mode
                return CurrentOperatingMode switch
                {
                    AwakeMode.PASSIVE => new
                    {
                        baseConfig.mode,
                        baseConfig.processId,
                        baseConfig.isUsingPowerToysConfig,
                    },
                    AwakeMode.INDEFINITE => new
                    {
                        baseConfig.mode,
                        baseConfig.keepDisplayOn,
                        baseConfig.processId,
                        baseConfig.isUsingPowerToysConfig,
                    },
                    AwakeMode.TIMED => new
                    {
                        baseConfig.mode,
                        baseConfig.keepDisplayOn,
                        timeRemaining = TimeRemaining,
                        intervalHours = currentSettings.Properties.IntervalHours,
                        intervalMinutes = currentSettings.Properties.IntervalMinutes,
                        baseConfig.processId,
                        baseConfig.isUsingPowerToysConfig,
                    },
                    AwakeMode.EXPIRABLE => new
                    {
                        baseConfig.mode,
                        baseConfig.keepDisplayOn,
                        expireAt = ExpireAt == DateTimeOffset.MinValue ? (DateTimeOffset?)null : ExpireAt,
                        expirationDateTime = currentSettings.Properties.ExpirationDateTime,
                        baseConfig.processId,
                        baseConfig.isUsingPowerToysConfig,
                    },
                    AwakeMode.ACTIVITY => new
                    {
                        baseConfig.mode,
                        baseConfig.keepDisplayOn,
                        cpuThresholdPercent = currentSettings.Properties.ActivityCpuThresholdPercent,
                        memoryThresholdPercent = currentSettings.Properties.ActivityMemoryThresholdPercent,
                        networkThresholdKBps = currentSettings.Properties.ActivityNetworkThresholdKBps,
                        sampleIntervalSeconds = currentSettings.Properties.ActivitySampleIntervalSeconds,
                        inactivityTimeoutSeconds = currentSettings.Properties.ActivityInactivityTimeoutSeconds,
                        activityActive = _activityActive,
                        lastHighActivity = _activityLastHigh,
                        baseConfig.processId,
                        baseConfig.isUsingPowerToysConfig,
                    },
                    AwakeMode.PROCESS => new
                    {
                        baseConfig.mode,
                        baseConfig.keepDisplayOn,
                        processMonitoringList = currentSettings.Properties.ProcessMonitoringList,
                        processCheckIntervalSeconds = currentSettings.Properties.ProcessCheckIntervalSeconds,
                        processMonitoringActive = _processMonitoringActive,
                        baseConfig.processId,
                        baseConfig.isUsingPowerToysConfig,
                    },
                    _ => new
                    {
                        baseConfig.mode,
                        baseConfig.keepDisplayOn,
                        baseConfig.processId,
                        baseConfig.isUsingPowerToysConfig,
                        error = "Unknown mode",
                    },
                };
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to get current config: {ex.Message}");
                return new { error = "Failed to get current configuration", message = ex.Message };
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
