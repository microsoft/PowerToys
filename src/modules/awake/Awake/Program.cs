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
using Awake.Properties;
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
        private static readonly string BuildId = "DAISY023_04102024";

        private static readonly ManualResetEvent _exitSignal = new(false);

        private static Mutex? _mutex;
        private static FileSystemWatcher? _watcher;
        private static SettingsUtils? _settingsUtils;

        private static bool _startedFromPowerToys;

        public static Mutex? LockMutex { get => _mutex; set => _mutex = value; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private static ConsoleEventHandler _handler;
        private static SystemPowerCapabilities _powerCapabilities;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        internal static readonly string[] AliasesConfigOption = ["--use-pt-config", "-c"];
        internal static readonly string[] AliasesDisplayOption = ["--display-on", "-d"];
        internal static readonly string[] AliasesTimeOption = ["--time-limit", "-t"];
        internal static readonly string[] AliasesPidOption = ["--pid", "-p"];
        internal static readonly string[] AliasesExpireAtOption = ["--expire-at", "-e"];

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

            var configOption = new Option<bool>(AliasesConfigOption, () => false, Resources.AWAKE_CMD_HELP_CONFIG_OPTION)
            {
                Arity = ArgumentArity.ZeroOrOne,
                IsRequired = false,
            };

            var displayOption = new Option<bool>(AliasesDisplayOption, () => true, Resources.AWAKE_CMD_HELP_DISPLAY_OPTION)
            {
                Arity = ArgumentArity.ZeroOrOne,
                IsRequired = false,
            };

            var timeOption = new Option<uint>(AliasesTimeOption, () => 0, Resources.AWAKE_CMD_HELP_TIME_OPTION)
            {
                Arity = ArgumentArity.ExactlyOne,
                IsRequired = false,
            };

            var pidOption = new Option<int>(AliasesPidOption, () => 0, Resources.AWAKE_CMD_HELP_PID_OPTION)
            {
                Arity = ArgumentArity.ZeroOrOne,
                IsRequired = false,
            };

            var expireAtOption = new Option<string>(AliasesExpireAtOption, () => string.Empty, Resources.AWAKE_CMD_HELP_EXPIREAT_OPTION)
            {
                Arity = ArgumentArity.ZeroOrOne,
                IsRequired = false,
            };

            RootCommand? rootCommand =
            [
                configOption,
                displayOption,
                timeOption,
                pidOption,
                expireAtOption,
            ];

            rootCommand.Description = Core.Constants.AppName;
            rootCommand.SetHandler(HandleCommandLineArguments, configOption, displayOption, timeOption, pidOption, expireAtOption);

            return rootCommand.InvokeAsync(args).Result;
        }

        private static bool ExitHandler(ControlType ctrlType)
        {
            Logger.LogInfo($"Exited through handler with control type: {ctrlType}");
            Exit(Resources.AWAKE_EXIT_MESSAGE, Environment.ExitCode, _exitSignal);
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
                Manager.IsUsingPowerToysConfig = true;

                try
                {
                    var eventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, interop.Constants.AwakeExitEvent());
                    new Thread(() =>
                    {
                        if (WaitHandle.WaitAny([_exitSignal, eventHandle]) == 1)
                        {
                            Exit(Resources.AWAKE_EXIT_SIGNAL_MESSAGE, 0, _exitSignal, true);
                        }
                    }).Start();

                    TrayHelper.InitializeTray(Core.Constants.FullAppName, new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Awake/awake.ico")), _exitSignal);

                    string? settingsPath = _settingsUtils!.GetSettingsFilePath(Core.Constants.AppName);

                    Logger.LogInfo($"Reading configuration file: {settingsPath}");

                    if (!File.Exists(settingsPath))
                    {
                        Logger.LogError("The settings file does not exist. Scaffolding default configuration...");

                        AwakeSettings scaffoldSettings = new();
                        _settingsUtils.SaveSettings(JsonSerializer.Serialize(scaffoldSettings), Core.Constants.AppName);
                    }

                    ScaffoldConfiguration(settingsPath);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"There was a problem with the configuration file. Make sure it exists.\n{ex.Message}");
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
                            Manager.SetExpirableKeepAwake(expirationDateTime, displayOn);
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
                        Manager.SetIndefiniteKeepAwake(displayOn);
                    }
                    else
                    {
                        Manager.SetTimedKeepAwake(timeLimit, displayOn);
                    }
                }
            }

            if (pid != 0)
            {
                RunnerHelper.WaitForPowerToysRunner(pid, () =>
                {
                    Logger.LogInfo($"Triggered PID-based exit handler for PID {pid}.");
                    Exit(Resources.AWAKE_EXIT_BINDINGHOOK_MESSAGE, 0, _exitSignal, true);
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

                TrayHelper.SetTray(Core.Constants.FullAppName, Manager.ModuleSettings!.GetSettings<AwakeSettings>(Core.Constants.AppName) ?? new AwakeSettings(), _startedFromPowerToys);

                // Initially the file might not be updated, so we need to start processing
                // settings right away.
                ProcessSettings();
            }
            catch (Exception ex)
            {
                Logger.LogError($"An error occurred scaffolding the configuration. Error details: {ex.Message}");
            }
        }

        private static void HandleAwakeConfigChange(FileSystemEventArgs fileEvent)
        {
            Logger.LogInfo("Detected a settings file change. Updating configuration...");
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
                                Manager.SetPassiveKeepAwake();
                                break;
                            }

                        case AwakeMode.INDEFINITE:
                            {
                                Manager.SetIndefiniteKeepAwake(settings.Properties.KeepDisplayOn);
                                break;
                            }

                        case AwakeMode.TIMED:
                            {
                                uint computedTime = (settings.Properties.IntervalHours * 60 * 60) + (settings.Properties.IntervalMinutes * 60);
                                Manager.SetTimedKeepAwake(computedTime, settings.Properties.KeepDisplayOn);

                                break;
                            }

                        case AwakeMode.EXPIRABLE:
                            {
                                Manager.SetExpirableKeepAwake(settings.Properties.ExpirationDateTime, settings.Properties.KeepDisplayOn);

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
    }
}
