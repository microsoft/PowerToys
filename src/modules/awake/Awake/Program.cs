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
using Microsoft.PowerToys.Telemetry;

namespace Awake
{
    internal sealed class Program
    {
        private static Mutex? _mutex;
        private static FileSystemWatcher? _watcher;
        private static SettingsUtils? _settingsUtils;
        private static ETWTrace _etwTrace = new ETWTrace();

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
        internal static readonly string[] AliasesParentPidOption = ["--use-parent-pid", "-u"];

        private static readonly Icon _defaultAwakeIcon = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets/Awake/awake.ico"));

        private static int Main(string[] args)
        {
            _etwTrace.Start();
            _settingsUtils = new SettingsUtils();
            LockMutex = new Mutex(true, Core.Constants.AppName, out bool instantiated);
            Logger.InitializeLogger(Path.Combine("\\", Core.Constants.AppName, "Logs"));

            try
            {
                string appLanguage = LanguageHelper.LoadLanguage();
                if (!string.IsNullOrEmpty(appLanguage))
                {
                    System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(appLanguage);
                }
            }
            catch (CultureNotFoundException ex)
            {
                Logger.LogError("CultureNotFoundException: " + ex.Message);
            }

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

                    var expireAtOption = new Option<string>(AliasesExpireAtOption, () => string.Empty, Resources.AWAKE_CMD_HELP_EXPIRE_AT_OPTION)
                    {
                        Arity = ArgumentArity.ZeroOrOne,
                        IsRequired = false,
                    };

                    var parentPidOption = new Option<bool>(AliasesParentPidOption, () => false, Resources.AWAKE_CMD_PARENT_PID_OPTION)
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

            TrayHelper.InitializeTray(_defaultAwakeIcon, Core.Constants.FullAppName);

            var eventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, PowerToys.Interop.Constants.AwakeExitEvent());
            new Thread(() =>
            {
                WaitHandle.WaitAny([eventHandle]);
                Exit(Resources.AWAKE_EXIT_SIGNAL_MESSAGE, 0);
            }).Start();

            if (usePtConfig)
            {
                // Configuration file is used, therefore we disregard any other command-line parameter
                // and instead watch for changes in the file.
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
                // Second, we snap to process-based execution. Because this is something that
                // is snapped to a running entity, we only want to enable the ability to set
                // indefinite keep-awake with the display settings that the user wants to set.
                int targetPid = pid != 0 ? pid : useParentPid ? Manager.GetParentProcess()?.Id ?? 0 : 0;

                if (targetPid != 0)
                {
                    Logger.LogInfo($"Bound to target process: {targetPid}");

                    Manager.SetIndefiniteKeepAwake(displayOn);

                    RunnerHelper.WaitForPowerToysRunner(targetPid, () =>
                    {
                        Logger.LogInfo($"Triggered PID-based exit handler for PID {targetPid}.");
                        Exit(Resources.AWAKE_EXIT_BINDING_HOOK_MESSAGE, 0);
                    });
                }
                else
                {
                    Logger.LogError("Not binding to any process.");
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
            var directory = Path.GetDirectoryName(settingsPath)!;
            var fileName = Path.GetFileName(settingsPath);

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
            var settings = Manager.ModuleSettings?.GetSettings<AwakeSettings>(Core.Constants.AppName) ?? new AwakeSettings();
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
                var settings = _settingsUtils!.GetSettings<AwakeSettings>(Core.Constants.AppName)
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
