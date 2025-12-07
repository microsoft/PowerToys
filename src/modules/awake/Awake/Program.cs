// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
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
using Microsoft.PowerToys.Telemetry;

namespace Awake
{
    internal sealed class Program
    {
        private static readonly string[] _aliasesConfigOption = ["--use-pt-config", "-c"];
        private static readonly string[] _aliasesDisplayOption = ["--display-on", "-d"];
        private static readonly string[] _aliasesTimeOption = ["--time-limit", "-t"];
        private static readonly string[] _aliasesPidOption = ["--pid", "-p"];
        private static readonly string[] _aliasesExpireAtOption = ["--expire-at", "-e"];
        private static readonly string[] _aliasesParentPidOption = ["--use-parent-pid", "-u"];

        private static readonly JsonSerializerOptions _serializerOptions = new() { IncludeFields = true };
        private static readonly ETWTrace _etwTrace = new();

        private static FileSystemWatcher? _watcher;
        private static SettingsUtils? _settingsUtils;

        private static bool _startedFromPowerToys;

        public static Mutex? LockMutex { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private static ConsoleEventHandler _handler;
        private static SystemPowerCapabilities _powerCapabilities;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        private static async Task<int> Main(string[] args)
        {
            _settingsUtils = SettingsUtils.Default;

            LockMutex = new Mutex(true, Core.Constants.AppName, out bool instantiated);

            Logger.InitializeLogger(Path.Combine("\\", Core.Constants.AppName, "Logs"));

            try
            {
                string appLanguage = LanguageHelper.LoadLanguage();
                if (!string.IsNullOrEmpty(appLanguage))
                {
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo(appLanguage);
                }
            }
            catch (CultureNotFoundException ex)
            {
                Logger.LogError("CultureNotFoundException: " + ex.Message);
            }

            await TrayHelper.InitializeTray(TrayHelper.DefaultAwakeIcon, Core.Constants.FullAppName);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => TrayHelper.RunOnMainThread(() => LockMutex?.ReleaseMutex());
            AppDomain.CurrentDomain.UnhandledException += AwakeUnhandledExceptionCatcher;

            if (!instantiated)
            {
                // Awake is already running - there is no need for us to process
                // anything further
                Exit(Core.Constants.AppName + " is already running! Exiting the application.", 1);
                return 1;
            }
            else
            {
                if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredAwakeEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
                {
                    Exit("PowerToys.Awake tried to start with a group policy setting that disables the tool. Please contact your system administrator.", 1);
                    return 1;
                }
                else
                {
                    Logger.LogInfo($"Launching {Core.Constants.AppName}...");
                    Logger.LogInfo(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
                    Logger.LogInfo($"Build: {Core.Constants.BuildId}");
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
                    Logger.LogInfo(JsonSerializer.Serialize(_powerCapabilities, _serializerOptions));

                    Logger.LogInfo("Parsing parameters...");

                    Option<bool> configOption = new(_aliasesConfigOption, () => false, Resources.AWAKE_CMD_HELP_CONFIG_OPTION)
                    {
                        Arity = ArgumentArity.ZeroOrOne,
                        IsRequired = false,
                    };

                    Option<bool> displayOption = new(_aliasesDisplayOption, () => true, Resources.AWAKE_CMD_HELP_DISPLAY_OPTION)
                    {
                        Arity = ArgumentArity.ZeroOrOne,
                        IsRequired = false,
                    };

                    Option<uint> timeOption = new(_aliasesTimeOption, () => 0, Resources.AWAKE_CMD_HELP_TIME_OPTION)
                    {
                        Arity = ArgumentArity.ExactlyOne,
                        IsRequired = false,
                    };

                    Option<int> pidOption = new(_aliasesPidOption, () => 0, Resources.AWAKE_CMD_HELP_PID_OPTION)
                    {
                        Arity = ArgumentArity.ZeroOrOne,
                        IsRequired = false,
                    };

                    Option<string> expireAtOption = new(_aliasesExpireAtOption, () => string.Empty, Resources.AWAKE_CMD_HELP_EXPIRE_AT_OPTION)
                    {
                        Arity = ArgumentArity.ZeroOrOne,
                        IsRequired = false,
                    };

                    Option<bool> parentPidOption = new(_aliasesParentPidOption, () => false, Resources.AWAKE_CMD_PARENT_PID_OPTION)
                    {
                        Arity = ArgumentArity.ZeroOrOne,
                        IsRequired = false,
                    };

                    timeOption.AddValidator(result =>
                    {
                        if (result.Tokens.Count != 0 && !uint.TryParse(result.Tokens[0].Value, out _))
                        {
                            string errorMessage = $"Interval in --time-limit could not be parsed correctly. Check that the value is valid and doesn't exceed 4,294,967,295. Value used: {result.Tokens[0].Value}.";
                            Logger.LogError(errorMessage);
                            result.ErrorMessage = errorMessage;
                        }
                    });

                    pidOption.AddValidator(result =>
                    {
                        if (result.Tokens.Count == 0)
                        {
                            return;
                        }

                        string tokenValue = result.Tokens[0].Value;

                        if (!int.TryParse(tokenValue, out int parsed))
                        {
                            string errorMessage = $"PID value in --pid could not be parsed correctly. Check that the value is valid and falls within the boundaries of Windows PID process limits. Value used: {tokenValue}.";
                            Logger.LogError(errorMessage);
                            result.ErrorMessage = errorMessage;
                            return;
                        }

                        if (parsed <= 0)
                        {
                            string errorMessage = $"PID value in --pid must be a positive integer. Value used: {parsed}.";
                            Logger.LogError(errorMessage);
                            result.ErrorMessage = errorMessage;
                            return;
                        }

                        // Process existence check. (We also re-validate just before binding.)
                        if (!ProcessExists(parsed))
                        {
                            string errorMessage = $"No running process found with an ID of {parsed}.";
                            Logger.LogError(errorMessage);
                            result.ErrorMessage = errorMessage;
                        }
                    });

                    expireAtOption.AddValidator(result =>
                    {
                        if (result.Tokens.Count != 0 && !DateTimeOffset.TryParse(result.Tokens[0].Value, out _))
                        {
                            string errorMessage = $"Date and time value in --expire-at could not be parsed correctly. Check that the value is valid date and time. Refer to https://aka.ms/powertoys/awake for format examples. Value used: {result.Tokens[0].Value}.";
                            Logger.LogError(errorMessage);
                            result.ErrorMessage = errorMessage;
                        }
                    });

                    RootCommand? rootCommand =
                    [
                        configOption,
                        displayOption,
                        timeOption,
                        pidOption,
                        expireAtOption,
                        parentPidOption,
                    ];

                    rootCommand.Description = Core.Constants.AppName;
                    rootCommand.SetHandler(HandleCommandLineArguments, configOption, displayOption, timeOption, pidOption, expireAtOption, parentPidOption);

                    return rootCommand.InvokeAsync(args).Result;
                }
            }
        }

        private static void AwakeUnhandledExceptionCatcher(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                Logger.LogError(exception.ToString());
                Logger.LogError(exception.StackTrace);
            }
        }

        private static bool ExitHandler(ControlType ctrlType)
        {
            Logger.LogInfo($"Exited through handler with control type: {ctrlType}");
            Exit(Resources.AWAKE_EXIT_MESSAGE, Environment.ExitCode);
            return false;
        }

        private static void Exit(string message, int exitCode)
        {
            _etwTrace?.Dispose();
            Logger.LogInfo(message);
            Manager.CompleteExit(exitCode);
        }

        private static bool ProcessExists(int processId)
        {
            if (processId <= 0)
            {
                return false;
            }

            try
            {
                // Throws if the Process ID is not found.
                using var p = Process.GetProcessById(processId);
                return !p.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private static void HandleCommandLineArguments(bool usePtConfig, bool displayOn, uint timeLimit, int pid, string expireAt, bool useParentPid)
        {
            if (pid == 0 && !useParentPid)
            {
                Logger.LogInfo("No PID specified. Allocating console...");
                AllocateLocalConsole();
            }
            else
            {
                Logger.LogInfo("Starting with PID binding.");
                _startedFromPowerToys = true;
            }

            Logger.LogInfo($"The value for --use-pt-config is: {usePtConfig}");
            Logger.LogInfo($"The value for --display-on is: {displayOn}");
            Logger.LogInfo($"The value for --time-limit is: {timeLimit}");
            Logger.LogInfo($"The value for --pid is: {pid}");
            Logger.LogInfo($"The value for --expire-at is: {expireAt}");
            Logger.LogInfo($"The value for --use-parent-pid is: {useParentPid}");

            // Start the monitor thread that will be used to track the current state.
            Manager.StartMonitor();

            EventWaitHandle eventHandle = new(false, EventResetMode.ManualReset, PowerToys.Interop.Constants.AwakeExitEvent());
            new Thread(() =>
            {
                WaitHandle.WaitAny([eventHandle]);
                Exit(Resources.AWAKE_EXIT_SIGNAL_MESSAGE, 0);
            }).Start();

            if (usePtConfig)
            {
                // Configuration file is used, therefore we disregard any other command-line parameter
                // and instead watch for changes in the file. This is used as a priority against all other arguments,
                // so if --use-pt-config is applied the rest of the arguments are irrelevant.
                Manager.IsUsingPowerToysConfig = true;

                try
                {
                    string? settingsPath = _settingsUtils!.GetSettingsFilePath(Core.Constants.AppName);

                    Logger.LogInfo($"Reading configuration file: {settingsPath}");

                    if (!File.Exists(settingsPath))
                    {
                        Logger.LogError("The settings file does not exist. Scaffolding default configuration...");

                        AwakeSettings scaffoldSettings = new();
                        _settingsUtils.SaveSettings(JsonSerializer.Serialize(scaffoldSettings), Core.Constants.AppName);
                    }

                    ScaffoldConfiguration(settingsPath);

                    if (pid != 0)
                    {
                        if (!ProcessExists(pid))
                        {
                            Logger.LogError($"PID {pid} does not exist or is not accessible. Exiting.");
                            Exit(Resources.AWAKE_EXIT_PROCESS_BINDING_FAILURE_MESSAGE, 1);
                        }

                        Logger.LogInfo($"Bound to target process while also using PowerToys settings: {pid}");

                        RunnerHelper.WaitForPowerToysRunner(pid, () =>
                        {
                            Logger.LogInfo($"Triggered PID-based exit handler for PID {pid}.");
                            Exit(Resources.AWAKE_EXIT_BINDING_HOOK_MESSAGE, 0);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"There was a problem with the configuration file. Make sure it exists. {ex.Message}");
                }
            }
            else if (pid != 0 || useParentPid)
            {
                HandleProcessScopedKeepAwake(pid, useParentPid, displayOn);
            }
            else
            {
                // Date-based binding takes precedence over timed configuration, so we want to
                // check for that first.
                if (!string.IsNullOrWhiteSpace(expireAt))
                {
                    try
                    {
                        DateTimeOffset expirationDateTime = DateTimeOffset.Parse(expireAt, CultureInfo.CurrentCulture);
                        Logger.LogInfo($"Operating in thread ID {Environment.CurrentManagedThreadId}.");
                        Manager.SetExpirableKeepAwake(expirationDateTime, displayOn);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Could not parse date string {expireAt} into a DateTimeOffset object.");
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
        }

        /// <summary>
        /// Start a process-scoped keep-awake session. The application will keep the system awake
        /// indefinitely until the target process terminates.
        /// </summary>
        /// <param name="pid">The explicit process ID to monitor.</param>
        /// <param name="useParentPid">A flag indicating whether the application should monitor its
        /// parent process.</param>
        /// <param name="displayOn">Whether to keep the display on during the session.</param>
        private static void HandleProcessScopedKeepAwake(int pid, bool useParentPid, bool displayOn)
        {
            int targetPid = 0;

            // We prioritize a user-provided PID over the parent PID. If both are given on the
            // command line, the --pid value will be used.
            if (pid != 0)
            {
                if (pid == Environment.ProcessId)
                {
                    Logger.LogError("Awake cannot bind to itself, as this would lead to an indefinite keep-awake state.");
                    Exit(Resources.AWAKE_EXIT_BIND_TO_SELF_FAILURE_MESSAGE, 1);
                }

                if (!ProcessExists(pid))
                {
                    Logger.LogError($"PID {pid} does not exist or is not accessible. Exiting.");
                    Exit(Resources.AWAKE_EXIT_PROCESS_BINDING_FAILURE_MESSAGE, 1);
                }

                targetPid = pid;
            }
            else if (useParentPid)
            {
                targetPid = Manager.GetParentProcess()?.Id ?? 0;

                if (targetPid == 0)
                {
                    // The parent process could not be identified.
                    Logger.LogError("Failed to identify a parent process for binding.");
                    Exit(Resources.AWAKE_EXIT_PARENT_BINDING_FAILURE_MESSAGE, 1);
                }
            }

            // We have a valid non-zero PID to monitor.
            Logger.LogInfo($"Bound to target process: {targetPid}");

            // Sets the keep-awake plan and updates the tray icon.
            Manager.SetIndefiniteKeepAwake(displayOn, targetPid);

            // Synchronize with the target process, and trigger Exit() when it finishes.
            RunnerHelper.WaitForPowerToysRunner(targetPid, () =>
            {
                Logger.LogInfo($"Triggered PID-based exit handler for PID {targetPid}.");
                Exit(Resources.AWAKE_EXIT_BINDING_HOOK_MESSAGE, 0);
            });
        }

        private static void AllocateLocalConsole()
        {
            Manager.AllocateConsole();

            _handler += new ConsoleEventHandler(ExitHandler);
            Manager.SetConsoleControlHandler(_handler, true);

            Trace.Listeners.Add(new ConsoleTraceListener());
        }

        private static void ScaffoldConfiguration(string settingsPath)
        {
            try
            {
                SetupFileSystemWatcher(settingsPath);
                InitializeSettings();
                ProcessSettings();
            }
            catch (Exception ex)
            {
                Logger.LogError($"An error occurred scaffolding the configuration. Error details: {ex.Message}");
            }
        }

        private static void SetupFileSystemWatcher(string settingsPath)
        {
            string directory = Path.GetDirectoryName(settingsPath)!;
            string fileName = Path.GetFileName(settingsPath);

            _watcher = new FileSystemWatcher
            {
                Path = directory,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                Filter = fileName,
            };

            Observable.Merge(
                Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                    h => _watcher.Changed += h,
                    h => _watcher.Changed -= h),
                Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                    h => _watcher.Created += h,
                    h => _watcher.Created -= h))
            .Throttle(TimeSpan.FromMilliseconds(25))
            .SubscribeOn(TaskPoolScheduler.Default)
            .Select(e => e.EventArgs)
            .Subscribe(HandleAwakeConfigChange);
        }

        private static void InitializeSettings()
        {
            AwakeSettings settings = Manager.ModuleSettings?.GetSettings<AwakeSettings>(Core.Constants.AppName) ?? new AwakeSettings();
            TrayHelper.SetTray(settings, _startedFromPowerToys);
        }

        private static void HandleAwakeConfigChange(FileSystemEventArgs fileEvent)
        {
            try
            {
                Logger.LogInfo("Detected a settings file change. Updating configuration...");
                ProcessSettings();
            }
            catch (Exception e)
            {
                Logger.LogError($"Could not handle Awake configuration change. Error: {e.Message}");
            }
        }

        private static void ProcessSettings()
        {
            try
            {
                AwakeSettings settings = _settingsUtils!.GetSettings<AwakeSettings>(Core.Constants.AppName)
                    ?? throw new InvalidOperationException("Settings are null.");

                Logger.LogInfo($"Identified custom time shortcuts for the tray: {settings.Properties.CustomTrayTimes.Count}");

                switch (settings.Properties.Mode)
                {
                    case AwakeMode.PASSIVE:
                        Manager.SetPassiveKeepAwake();
                        break;

                    case AwakeMode.INDEFINITE:
                        Manager.SetIndefiniteKeepAwake(settings.Properties.KeepDisplayOn);
                        break;

                    case AwakeMode.TIMED:
                        uint computedTime = (settings.Properties.IntervalHours * 3600) + (settings.Properties.IntervalMinutes * 60);
                        Manager.SetTimedKeepAwake(computedTime, settings.Properties.KeepDisplayOn);
                        break;

                    case AwakeMode.EXPIRABLE:
                        // When we are loading from the settings file, let's make sure that we never
                        // get users in a state where the expirable keep-awake is in the past.
                        if (settings.Properties.ExpirationDateTime <= DateTimeOffset.Now)
                        {
                            settings.Properties.ExpirationDateTime = DateTimeOffset.Now.AddMinutes(5);
                            _settingsUtils.SaveSettings(JsonSerializer.Serialize(settings), Core.Constants.AppName);
                        }

                        Manager.SetExpirableKeepAwake(settings.Properties.ExpirationDateTime, settings.Properties.KeepDisplayOn);
                        break;

                    default:
                        Logger.LogError("Unknown mode of operation. Check config file.");
                        break;
                }

                TrayHelper.SetTray(settings, _startedFromPowerToys);
            }
            catch (Exception ex)
            {
                Logger.LogError($"There was a problem reading the configuration file. Error: {ex.GetType()} {ex.Message}");
            }
        }
    }
}
