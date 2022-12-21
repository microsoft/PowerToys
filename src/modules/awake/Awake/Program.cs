// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Awake.Core;
using interop;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using NLog;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Console;
using Windows.Win32.System.Power;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8603 // Possible null reference return.

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
        private static readonly string BuildId = "ARBITER_01312022";

        private static Mutex? _mutex;
        private static FileSystemWatcher? _watcher;
        private static SettingsUtils? _settingsUtils;

        private static bool _startedFromPowerToys;

        public static Mutex LockMutex { get => _mutex; set => _mutex = value; }

        private static Logger? _log;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private static PHANDLER_ROUTINE _handler;
        private static SYSTEM_POWER_CAPABILITIES _powerCapabilities;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        private static ManualResetEvent _exitSignal = new ManualResetEvent(false);

        private static int Main(string[] args)
        {
            // Log initialization needs to always happen before we test whether
            // only one instance of Awake is running.
            _log = LogManager.GetCurrentClassLogger();

            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredAwakeEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                Exit("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.", 1, _exitSignal, true);
                return 0;
            }

            LockMutex = new Mutex(true, InternalConstants.AppName, out bool instantiated);

            if (!instantiated)
            {
                Exit(InternalConstants.AppName + " is already running! Exiting the application.", 1, _exitSignal, true);
            }

            _settingsUtils = new SettingsUtils();

            _log.Info($"Launching {InternalConstants.AppName}...");
            _log.Info(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
            _log.Info($"Build: {BuildId}");
            _log.Info($"OS: {Environment.OSVersion}");
            _log.Info($"OS Build: {APIHelper.GetOperatingSystemBuild()}");

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Trace.WriteLine($"Task scheduler error: {args.Exception.Message}"); // somebody forgot to check!
                args.SetObserved();
            };

            // To make it easier to diagnose future issues, let's get the
            // system power capabilities and aggregate them in the log.
            PInvoke.GetPwrCapabilities(out _powerCapabilities);
            _log.Info(JsonSerializer.Serialize(_powerCapabilities));

            _log.Info("Parsing parameters...");

            var configOption = new Option<bool>(
                    aliases: new[] { "--use-pt-config", "-c" },
                    getDefaultValue: () => false,
                    description: $"Specifies whether {InternalConstants.AppName} will be using the PowerToys configuration file for managing the state.")
            {
                Argument = new Argument<bool>(() => false)
                {
                    Arity = ArgumentArity.ZeroOrOne,
                },
            };

            configOption.Required = false;

            var displayOption = new Option<bool>(
                    aliases: new[] { "--display-on", "-d" },
                    getDefaultValue: () => true,
                    description: "Determines whether the display should be kept awake.")
            {
                Argument = new Argument<bool>(() => false)
                {
                    Arity = ArgumentArity.ZeroOrOne,
                },
            };

            displayOption.Required = false;

            var timeOption = new Option<uint>(
                    aliases: new[] { "--time-limit", "-t" },
                    getDefaultValue: () => 0,
                    description: "Determines the interval, in seconds, during which the computer is kept awake.")
            {
                Argument = new Argument<uint>(() => 0)
                {
                    Arity = ArgumentArity.ExactlyOne,
                },
            };

            timeOption.Required = false;

            var pidOption = new Option<int>(
                    aliases: new[] { "--pid", "-p" },
                    getDefaultValue: () => 0,
                    description: $"Bind the execution of {InternalConstants.AppName} to another process.")
            {
                Argument = new Argument<int>(() => 0)
                {
                    Arity = ArgumentArity.ZeroOrOne,
                },
            };

            pidOption.Required = false;

            RootCommand? rootCommand = new RootCommand
            {
                configOption,
                displayOption,
                timeOption,
                pidOption,
            };

            rootCommand.Description = InternalConstants.AppName;

            rootCommand.Handler = CommandHandler.Create<bool, bool, uint, int>(HandleCommandLineArguments);

            _log.Info("Parameter setup complete. Proceeding to the rest of the app initiation...");

            return rootCommand.InvokeAsync(args).Result;
        }

        private static BOOL ExitHandler(uint ctrlType)
        {
            _log.Info($"Exited through handler with control type: {ctrlType}");
            Exit("Exiting from the internal termination handler.", Environment.ExitCode, _exitSignal);
            return false;
        }

        private static void Exit(string message, int exitCode, ManualResetEvent exitSignal, bool force = false)
        {
            _log.Info(message);

            APIHelper.CompleteExit(exitCode, exitSignal, force);
        }

        private static void HandleCommandLineArguments(bool usePtConfig, bool displayOn, uint timeLimit, int pid)
        {
            _handler += ExitHandler;
            APIHelper.SetConsoleControlHandler(_handler, true);

            if (pid == 0)
            {
                _log.Info("No PID specified. Allocating console...");
                APIHelper.AllocateConsole();
            }
            else
            {
                _startedFromPowerToys = true;
            }

            _log.Info($"The value for --use-pt-config is: {usePtConfig}");
            _log.Info($"The value for --display-on is: {displayOn}");
            _log.Info($"The value for --time-limit is: {timeLimit}");
            _log.Info($"The value for --pid is: {pid}");

            if (usePtConfig)
            {
                // Configuration file is used, therefore we disregard any other command-line parameter
                // and instead watch for changes in the file.
                try
                {
                    var eventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, Constants.AwakeExitEvent());
                    new Thread(() =>
                    {
                        if (WaitHandle.WaitAny(new WaitHandle[] { _exitSignal, eventHandle }) == 1)
                        {
                            Exit("Received a signal to end the process. Making sure we quit...", 0, _exitSignal, true);
                        }
                    }).Start();
                    TrayHelper.InitializeTray(InternalConstants.FullAppName, new Icon("modules/awake/images/awake.ico"), _exitSignal);

                    string? settingsPath = _settingsUtils.GetSettingsFilePath(InternalConstants.AppName);
                    _log.Info($"Reading configuration file: {settingsPath}");

                    _watcher = new FileSystemWatcher
                    {
#pragma warning disable CS8601 // Possible null reference assignment.
                        Path = Path.GetDirectoryName(settingsPath),
#pragma warning restore CS8601 // Possible null reference assignment.
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

                    TrayHelper.SetTray(InternalConstants.FullAppName, new AwakeSettings(), _startedFromPowerToys);

                    // Initially the file might not be updated, so we need to start processing
                    // settings right away.
                    ProcessSettings();
                }
                catch (Exception ex)
                {
                    string? errorString = $"There was a problem with the configuration file. Make sure it exists.\n{ex.Message}";
                    _log.Info(errorString);
                    _log.Debug(errorString);
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

            if (pid != 0)
            {
                RunnerHelper.WaitForPowerToysRunner(pid, () =>
                {
                    _log.Info($"Triggered PID-based exit handler for PID {pid}.");
                    Exit("Terminating from process binding hook.", 0, _exitSignal, true);
                });
            }

            _exitSignal.WaitOne();
        }

        private static void SetupIndefiniteKeepAwake(bool displayOn)
        {
            APIHelper.SetIndefiniteKeepAwake(LogCompletedKeepAwakeThread, LogUnexpectedOrCancelledKeepAwakeThreadCompletion, displayOn);
        }

        private static void HandleAwakeConfigChange(FileSystemEventArgs fileEvent)
        {
            _log.Info("Detected a settings file change. Updating configuration...");
            _log.Info("Resetting keep-awake to normal state due to settings change.");
            ProcessSettings();
        }

        private static void ProcessSettings()
        {
            try
            {
                AwakeSettings settings = _settingsUtils.GetSettings<AwakeSettings>(InternalConstants.AppName);

                if (settings != null)
                {
                    _log.Info($"Identified custom time shortcuts for the tray: {settings.Properties.TrayTimeShortcuts.Count}");

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
                                uint computedTime = (settings.Properties.Hours * 60 * 60) + (settings.Properties.Minutes * 60);
                                SetupTimedKeepAwake(computedTime, settings.Properties.KeepDisplayOn);

                                break;
                            }

                        default:
                            {
                                string? errorMessage = "Unknown mode of operation. Check config file.";
                                _log.Info(errorMessage);
                                _log.Debug(errorMessage);
                                break;
                            }
                    }

                    TrayHelper.SetTray(InternalConstants.FullAppName, settings, _startedFromPowerToys);
                }
                else
                {
                    string? errorMessage = "Settings are null.";
                    _log.Info(errorMessage);
                    _log.Debug(errorMessage);
                }
            }
            catch (Exception ex)
            {
                string? errorMessage = $"There was a problem reading the configuration file. Error: {ex.GetType()} {ex.Message}";
                _log.Info(errorMessage);
                _log.Debug(errorMessage);
            }
        }

        private static void SetupNoKeepAwake()
        {
            _log.Info($"Operating in passive mode (computer's standard power plan). No custom keep awake settings enabled.");

            APIHelper.SetNoKeepAwake();
        }

        private static void SetupTimedKeepAwake(uint time, bool displayOn)
        {
            _log.Info($"Timed keep-awake. Expected runtime: {time} seconds with display on setting set to {displayOn}.");

            APIHelper.SetTimedKeepAwake(time, LogCompletedKeepAwakeThread, LogUnexpectedOrCancelledKeepAwakeThreadCompletion, displayOn);
        }

        private static void LogUnexpectedOrCancelledKeepAwakeThreadCompletion()
        {
            string? errorMessage = "The keep-awake thread was terminated early.";
            _log.Info(errorMessage);
            _log.Debug(errorMessage);
        }

        private static void LogCompletedKeepAwakeThread(bool result)
        {
            _log.Info($"Exited keep-awake thread successfully: {result}");
        }
    }
}
