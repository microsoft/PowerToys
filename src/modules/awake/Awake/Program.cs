// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Awake.Core;
using Awake.Core.Models;
using Awake.Core.Native;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Awake
{
    internal sealed class Program
    {
        // PowerToys Awake build code name. Used for exact logging
        // that does not map to PowerToys broad version schema to pinpoint
        // internal issues easier.
        // Format of the build ID is: CODENAME_MMDDYYYY, where MMDDYYYY
        // is representative of the date when the last change was made before
        // the pull request is issued.
        private static readonly string BuildId = "ATRIOX_04132023";

        private static Mutex? _mutex;
        private static FileSystemWatcher? _watcher;
        private static SettingsUtils? _settingsUtils;

        private static bool _startedFromPowerToys;

        public static Mutex? LockMutex { get => _mutex; set => _mutex = value; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private static ConsoleEventHandler _handler;
        private static SystemPowerCapabilities _powerCapabilities;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        private static ManualResetEvent _exitSignal = new ManualResetEvent(false);
        internal static readonly string[] AliasesConfigOption = new[] { "--use-pt-config", "-c" };
        internal static readonly string[] AliasesDisplayOption = new[] { "--display-on", "-d" };
        internal static readonly string[] AliasesTimeOption = new[] { "--time-limit", "-t" };
        internal static readonly string[] AliasesPidOption = new[] { "--pid", "-p" };
        internal static readonly string[] AliasesExpireAtOption = new[] { "--expire-at", "-e" };

        private static int Main(string[] args)
        {
            _settingsUtils = new SettingsUtils();
            LockMutex = new Mutex(true, Core.Constants.AppName, out bool instantiated);

            Logger.InitializeLogger(Path.Combine("\\", Core.Constants.AppName, "Logs"));

            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredAwakeEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                Exit("PowerToys.Awake tried to start with a group policy setting that disables the tool. Please contact your system administrator.", 1, _exitSignal, true);
                return 0;
            }

            if (!instantiated)
            {
                Exit(Core.Constants.AppName + " is already running! Exiting the application.", 1, _exitSignal, true);
            }

            Logger.LogInfo($"Launching {Core.Constants.AppName}...");
            Logger.LogInfo(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
            Logger.LogInfo($"Build: {BuildId}");
            Logger.LogInfo($"OS: {Environment.OSVersion}");
            Logger.LogInfo($"OS Build: {Manager.GetOperatingSystemBuild()}");

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Trace.WriteLine($"Task scheduler error: {args.Exception.Message}"); // somebody forgot to check!
                args.SetObserved();
            };

            // To make it easier to diagnose future issues, let's get the
            // system power capabilities and aggregate them in the log.
            Bridge.GetPwrCapabilities(out _powerCapabilities);
            Logger.LogInfo(JsonSerializer.Serialize(_powerCapabilities));

            Logger.LogInfo("Parsing parameters...");

            Option<bool> configOption = new(
                    aliases: AliasesConfigOption,
                    getDefaultValue: () => false,
                    description: $"Specifies whether {Core.Constants.AppName} will be using the PowerToys configuration file for managing the state.")
            {
                Arity = ArgumentArity.ZeroOrOne,
                IsRequired = false,
            };

            Option<bool> displayOption = new(
                    aliases: AliasesDisplayOption,
                    getDefaultValue: () => true,
                    description: "Determines whether the display should be kept awake.")
            {
                Arity = ArgumentArity.ZeroOrOne,
                IsRequired = false,
            };

            Option<uint> timeOption = new(
                    aliases: AliasesTimeOption,
                    getDefaultValue: () => 0,
                    description: "Determines the interval, in seconds, during which the computer is kept awake.")
            {
                Arity = ArgumentArity.ExactlyOne,
                IsRequired = false,
            };

            Option<int> pidOption = new(
                    aliases: AliasesPidOption,
                    getDefaultValue: () => 0,
                    description: $"Bind the execution of {Core.Constants.AppName} to another process. When the process ends, the system will resume managing the current sleep and display state.")
            {
                Arity = ArgumentArity.ZeroOrOne,
                IsRequired = false,
            };

            Option<string> expireAtOption = new(
                    aliases: AliasesExpireAtOption,
                    getDefaultValue: () => string.Empty,
                    description: $"Determines the end date/time when {Core.Constants.AppName} will back off and let the system manage the current sleep and display state.")
            {
                Arity = ArgumentArity.ZeroOrOne,
                IsRequired = false,
            };

            RootCommand? rootCommand = new()
            {
                configOption,
                displayOption,
                timeOption,
                pidOption,
                expireAtOption,
            };

            rootCommand.Description = Core.Constants.AppName;

            rootCommand.SetHandler(
                HandleCommandLineArguments,
                configOption,
                displayOption,
                timeOption,
                pidOption,
                expireAtOption);

            return rootCommand.InvokeAsync(args).Result;
        }

        private static bool ExitHandler(ControlType ctrlType)
        {
            Logger.LogInfo($"Exited through handler with control type: {ctrlType}");
            Exit("Exiting from the internal termination handler.", Environment.ExitCode, _exitSignal);
            return false;
        }

        private static void Exit(string message, int exitCode, ManualResetEvent exitSignal, bool force = false)
        {
            Logger.LogInfo(message);

            Manager.CompleteExit(exitCode, exitSignal, force);
        }

        private static void HandleCommandLineArguments(bool usePtConfig, bool displayOn, uint timeLimit, int pid, string expireAt)
        {
            if (pid == 0)
            {
                Logger.LogInfo("No PID specified. Allocating console...");
                Manager.AllocateConsole();

                _handler += new ConsoleEventHandler(ExitHandler);
                Manager.SetConsoleControlHandler(_handler, true);

                Trace.Listeners.Add(new ConsoleTraceListener());
            }
            else
            {
                _startedFromPowerToys = true;
            }

            Logger.LogInfo($"The value for --use-pt-config is: {usePtConfig}");
            Logger.LogInfo($"The value for --display-on is: {displayOn}");
            Logger.LogInfo($"The value for --time-limit is: {timeLimit}");
            Logger.LogInfo($"The value for --pid is: {pid}");
            Logger.LogInfo($"The value for --expire-at is: {expireAt}");

            // Start the monitor thread that will be used to track the current state.
            Manager.StartMonitor();

            if (usePtConfig)
            {
                // Configuration file is used, therefore we disregard any other command-line parameter
                // and instead watch for changes in the file.
                try
                {
                    var eventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, interop.Constants.AwakeExitEvent());
                    new Thread(() =>
                    {
                        if (WaitHandle.WaitAny(new WaitHandle[] { _exitSignal, eventHandle }) == 1)
                        {
                            Exit("Received a signal to end the process. Making sure we quit...", 0, _exitSignal, true);
                        }
                    }).Start();

                    TrayHelper.InitializeTray(Core.Constants.FullAppName, new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Awake/awake.ico")), _exitSignal);

                    string? settingsPath = _settingsUtils!.GetSettingsFilePath(Core.Constants.AppName);
                    Logger.LogInfo($"Reading configuration file: {settingsPath}");

                    if (!File.Exists(settingsPath))
                    {
                        string? errorString = $"The settings file does not exist. Scaffolding default configuration...";

                        AwakeSettings scaffoldSettings = new AwakeSettings();
                        _settingsUtils.SaveSettings(JsonSerializer.Serialize(scaffoldSettings), Core.Constants.AppName);
                    }

                    ScaffoldConfiguration(settingsPath);
                }
                catch (Exception ex)
                {
                    string? errorString = $"There was a problem with the configuration file. Make sure it exists.\n{ex.Message}";
                    Logger.LogError(errorString);
                }
            }
            else
            {
                // Date-based binding takes precedence over timed configuration, so we want to
                // check for that first.
                if (!string.IsNullOrWhiteSpace(expireAt))
                {
                    try
                    {
                        DateTime expirationDateTime = DateTime.Parse(expireAt, CultureInfo.CurrentCulture);
                        if (expirationDateTime > DateTime.Now)
                        {
                            // We want to have a dedicated expirable keep-awake logic instead of
                            // converting the target date to seconds and then passing to SetupTimedKeepAwake
                            // because that way we're accounting for the user potentially changing their clock
                            // while Awake is running.
                            Logger.LogInfo($"Operating in thread ID {Environment.CurrentManagedThreadId}.");
                            SetupExpirableKeepAwake(expirationDateTime, displayOn);
                        }
                        else
                        {
                            Logger.LogInfo($"Target date is not in the future, therefore there is nothing to wait for.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Could not parse date string {expireAt} into a viable date.");
                        Logger.LogError(ex.Message);
                    }
                }
                else
                {
                    AwakeMode mode = timeLimit <= 0 ? AwakeMode.INDEFINITE : AwakeMode.TIMED;

                    if (mode == AwakeMode.INDEFINITE)
                    {
                        SetupIndefiniteKeepAwake(displayOn);
                    }
                    else
                    {
                        SetupTimedKeepAwake(timeLimit, displayOn);
                    }
                }
            }

            if (pid != 0)
            {
                RunnerHelper.WaitForPowerToysRunner(pid, () =>
                {
                    Logger.LogInfo($"Triggered PID-based exit handler for PID {pid}.");
                    Exit("Terminating from process binding hook.", 0, _exitSignal, true);
                });
            }

            _exitSignal.WaitOne();
        }

        private static void ScaffoldConfiguration(string settingsPath)
        {
            try
            {
                _watcher = new FileSystemWatcher
                {
                    Path = Path.GetDirectoryName(settingsPath)!,
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                    Filter = Path.GetFileName(settingsPath),
                };

                IObservable<System.Reactive.EventPattern<FileSystemEventArgs>>? changedObservable = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                        h => _watcher.Changed += h,
                        h => _watcher.Changed -= h);

                IObservable<System.Reactive.EventPattern<FileSystemEventArgs>>? createdObservable = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                        cre => _watcher.Created += cre,
                        cre => _watcher.Created -= cre);

                IObservable<System.Reactive.EventPattern<FileSystemEventArgs>>? mergedObservable = Observable.Merge(changedObservable, createdObservable);

                mergedObservable.Throttle(TimeSpan.FromMilliseconds(25))
                    .SubscribeOn(TaskPoolScheduler.Default)
                    .Select(e => e.EventArgs)
                    .Subscribe(HandleAwakeConfigChange);

                TrayHelper.SetTray(Core.Constants.FullAppName, new AwakeSettings(), _startedFromPowerToys);

                // Initially the file might not be updated, so we need to start processing
                // settings right away.
                ProcessSettings();
            }
            catch (Exception ex)
            {
                Logger.LogError($"An error occurred scaffolding the configuration. Error details: {ex.Message}");
            }
        }

        private static void SetupIndefiniteKeepAwake(bool displayOn)
        {
            Manager.SetIndefiniteKeepAwake(displayOn);
        }

        private static void HandleAwakeConfigChange(FileSystemEventArgs fileEvent)
        {
            Logger.LogInfo("Detected a settings file change. Updating configuration...");
            Logger.LogInfo("Resetting keep-awake to normal state due to settings change.");
            ProcessSettings();
        }

        private static void ProcessSettings()
        {
            try
            {
                AwakeSettings settings = _settingsUtils!.GetSettings<AwakeSettings>(Core.Constants.AppName);

                if (settings != null)
                {
                    Logger.LogInfo($"Identified custom time shortcuts for the tray: {settings.Properties.CustomTrayTimes.Count}");

                    switch (settings.Properties.Mode)
                    {
                        case AwakeMode.PASSIVE:
                            {
                                SetupNoKeepAwake();
                                break;
                            }

                        case AwakeMode.INDEFINITE:
                            {
                                SetupIndefiniteKeepAwake(settings.Properties.KeepDisplayOn);
                                break;
                            }

                        case AwakeMode.TIMED:
                            {
                                uint computedTime = (settings.Properties.IntervalHours * 60 * 60) + (settings.Properties.IntervalMinutes * 60);
                                SetupTimedKeepAwake(computedTime, settings.Properties.KeepDisplayOn);

                                break;
                            }

                        case AwakeMode.EXPIRABLE:
                            {
                                SetupExpirableKeepAwake(settings.Properties.ExpirationDateTime, settings.Properties.KeepDisplayOn);

                                break;
                            }

                        default:
                            {
                                string? errorMessage = "Unknown mode of operation. Check config file.";
                                Logger.LogError(errorMessage);
                                break;
                            }
                    }

                    TrayHelper.SetTray(Core.Constants.FullAppName, settings, _startedFromPowerToys);
                }
                else
                {
                    string? errorMessage = "Settings are null.";
                    Logger.LogError(errorMessage);
                }
            }
            catch (Exception ex)
            {
                string? errorMessage = $"There was a problem reading the configuration file. Error: {ex.GetType()} {ex.Message}";
                Logger.LogError(errorMessage);
            }
        }

        private static void SetupNoKeepAwake()
        {
            Logger.LogInfo($"Operating in passive mode (computer's standard power plan). No custom keep awake settings enabled.");

            Manager.SetNoKeepAwake();
        }

        private static void SetupExpirableKeepAwake(DateTimeOffset expireAt, bool displayOn)
        {
            Logger.LogInfo($"Expirable keep-awake. Expected expiration date/time: {expireAt} with display on setting set to {displayOn}.");

            Manager.SetExpirableKeepAwake(expireAt, displayOn);
        }

        private static void SetupTimedKeepAwake(uint time, bool displayOn)
        {
            Logger.LogInfo($"Timed keep-awake. Expected runtime: {time} seconds with display on setting set to {displayOn}.");

            Manager.SetTimedKeepAwake(time, displayOn);
        }
    }
}
