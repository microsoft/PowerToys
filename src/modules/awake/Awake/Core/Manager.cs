// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        internal static void SetExpirableKeepAwake(DateTimeOffset expireAt, bool keepDisplayOn = true)
        {
            Logger.LogInfo($"Expirable keep-awake. Expected expiration date/time: {expireAt} with display on setting set to {keepDisplayOn}.");

            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AwakeExpirableKeepAwakeEvent());

            CancelExistingThread();

            if (expireAt > DateTimeOffset.Now)
            {
                Logger.LogInfo($"Starting expirable log for {expireAt}");
                _stateQueue.Add(ComputeAwakeState(keepDisplayOn));

                Observable.Timer(expireAt - DateTimeOffset.Now).Subscribe(
                _ =>
                {
                    Logger.LogInfo($"Completed expirable keep-awake.");
                    CancelExistingThread();
                    SetPassiveKeepAwake();
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

            Observable.Timer(TimeSpan.FromSeconds(seconds)).Subscribe(
            _ =>
            {
                Logger.LogInfo($"Completed timed thread.");
                CancelExistingThread();
                SetPassiveKeepAwake();
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

        internal static void CompleteExit(int exitCode, ManualResetEvent? exitSignal, bool force = false)
        {
            SetPassiveKeepAwake();

            IntPtr windowHandle = GetHiddenWindow();

            if (windowHandle != IntPtr.Zero)
            {
                Bridge.SendMessage(windowHandle, Native.Constants.WM_CLOSE, 0, 0);
            }

            if (force)
            {
                Bridge.PostQuitMessage(exitCode);
            }

            try
            {
                exitSignal?.Set();
                Bridge.DestroyWindow(windowHandle);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exit signal error ${ex}");
            }
        }

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

        [SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "Function returns DWORD value that identifies the current thread, but we do not need it.")]
        internal static IEnumerable<IntPtr> EnumerateWindowsForProcess(int processId)
        {
            var handles = new List<IntPtr>();
            var hCurrentWnd = IntPtr.Zero;

            do
            {
                hCurrentWnd = Bridge.FindWindowEx(IntPtr.Zero, hCurrentWnd, null as string, null);
                Bridge.GetWindowThreadProcessId(hCurrentWnd, out uint targetProcessId);

                if (targetProcessId == processId)
                {
                    handles.Add(hCurrentWnd);
                }
            }
            while (hCurrentWnd != IntPtr.Zero);

            return handles;
        }

        [SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "In this context, the string is only converted to a hex value.")]
        internal static IntPtr GetHiddenWindow()
        {
            IEnumerable<IntPtr> windowHandles = EnumerateWindowsForProcess(Environment.ProcessId);
            var domain = AppDomain.CurrentDomain.GetHashCode().ToString("x");
            string targetClass = $"{Constants.TrayWindowId}{domain}";

            foreach (var handle in windowHandles)
            {
                StringBuilder className = new(256);
                int classQueryResult = Bridge.GetClassName(handle, className, className.Capacity);
                if (classQueryResult != 0 && className.ToString().StartsWith(targetClass, StringComparison.InvariantCultureIgnoreCase))
                {
                    return handle;
                }
            }

            return IntPtr.Zero;
        }

        internal static Dictionary<string, int> GetDefaultTrayOptions()
        {
            Dictionary<string, int> optionsList = new()
            {
                { string.Format(CultureInfo.InvariantCulture, AwakeMinutes, 30), 1800 },
                { Resources.AWAKE_1_HOUR, 3600 },
                { string.Format(CultureInfo.InvariantCulture, AwakeHours, 2), 7200 },
            };
            return optionsList;
        }

        internal static void SetPassiveKeepAwake()
        {
            Logger.LogInfo($"Operating in passive mode (computer's standard power plan). No custom keep awake settings enabled.");

            PowerToysTelemetry.Log.WriteEvent(new Telemetry.AwakeNoKeepAwakeEvent());

            CancelExistingThread();

            if (IsUsingPowerToysConfig)
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
