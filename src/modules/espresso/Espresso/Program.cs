// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Espresso.Shell.Core;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Newtonsoft.Json;
using NLog;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8603 // Possible null reference return.

namespace Espresso.Shell
{
    class Program
    {
        private static Mutex? mutex = null;
        private const string appName = "Espresso";
        private static FileSystemWatcher? watcher = null;
        private static SettingsUtils? settingsUtils = null;

        public static Mutex Mutex { get => mutex; set => mutex = value; }

        private static Logger? log;

        static int Main(string[] args)
        {
            bool instantiated;
            Mutex = new Mutex(true, appName, out instantiated);

            if (!instantiated)
            {
                ForceExit(appName + " is already running! Exiting the application.", 1);
            }

            log = LogManager.GetCurrentClassLogger();
            settingsUtils = new SettingsUtils();

            log.Info("Launching Espresso...");
            log.Info(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
            log.Debug($"OS: {Environment.OSVersion}");
            log.Debug($"OS Build: {APIHelper.GetOperatingSystemBuild()}");

            var configOption = new Option<bool>(
                    aliases: new[] { "--use-pt-config", "-c" },
                    getDefaultValue: () => true,
                    description: "Specifies whether Espresso will be using the PowerToys configuration file for managing the state.")
            {
                Argument = new Argument<bool>(() => true)
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

            var timeOption = new Option<long>(
                    aliases: new[] { "--time-limit", "-t" },
                    getDefaultValue: () => 0,
                    description: "Determines the interval, in seconds, during which the computer is kept awake.")
            {
                Argument = new Argument<long>(() => 0)
                {
                    Arity = ArgumentArity.ExactlyOne,
                },
            };

            timeOption.Required = false;

            var pidOption = new Option<int>(
                    aliases: new[] { "--pid", "-p" },
                    getDefaultValue: () => 0,
                    description: "Bind the execution of Espresso to another process.")
            {
                Argument = new Argument<int>(() => 0)
                {
                    Arity = ArgumentArity.ZeroOrOne,
                },
            };

            pidOption.Required = false;

            var rootCommand = new RootCommand
            {
                configOption,
                displayOption,
                timeOption,
                pidOption
            };

            rootCommand.Description = appName;

            rootCommand.Handler = CommandHandler.Create<bool, bool, long, int>(HandleCommandLineArguments);

            return rootCommand.InvokeAsync(args).Result;
        }

        private static void ForceExit(string message, int exitCode)
        {
            log.Debug(message);
            log.Info(message);
            Console.ReadKey();
            Environment.Exit(exitCode);
        }

        private static void HandleCommandLineArguments(bool usePtConfig, bool displayOn, long timeLimit, int pid)
        {
            log.Info($"The value for --use-pt-config is: {usePtConfig}");
            log.Info($"The value for --display-on is: {displayOn}");
            log.Info($"The value for --time-limit is: {timeLimit}");
            log.Info($"The value for --pid is: {pid}");

            if (usePtConfig)
            {
                // Configuration file is used, therefore we disregard any other command-line parameter
                // and instead watch for changes in the file.
                try
                {
                    var settingsPath = settingsUtils.GetSettingsFilePath(appName);
                    log.Info($"Reading configuration file: {settingsPath}");

                    watcher = new FileSystemWatcher
                    {
                        Path = Path.GetDirectoryName(settingsPath),
                        EnableRaisingEvents = true,
                        NotifyFilter = NotifyFilters.LastWrite,
                        Filter = Path.GetFileName(settingsPath)
                    };

                    Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                            h => watcher.Changed += h,
                            h => watcher.Changed -= h
                        )
                        .SubscribeOn(TaskPoolScheduler.Default)
                        .Select(e => e.EventArgs)
                        .Throttle(TimeSpan.FromMilliseconds(25))
                        .Subscribe(HandleEspressoConfigChange);

                    // Initially the file might not be updated, so we need to start processing
                    // settings right away.
                    ProcessSettings();
                }
                catch (Exception ex)
                {
                    var errorString = $"There was a problem with the configuration file. Make sure it exists.\n{ex.Message}";
                    log.Info(errorString);
                    log.Debug(errorString);
                }
            }
            else
            {
                if (timeLimit <= 0)
                {
                    SetupIndefiniteKeepAwake(displayOn);
                }
                else
                {
                    // Timed keep-awake.
                    SetupTimedKeepAwake(timeLimit, displayOn);
                }
            }

            if (pid != 0)
            {
                RunnerHelper.WaitForPowerToysRunner(pid, () =>
                {
                    Environment.Exit(0);
                });
            }

#pragma warning disable CS8604 // Possible null reference argument.
            TrayHelper.InitializeTray(appName, APIHelper.Extract("shell32.dll", 21, true), null);
#pragma warning restore CS8604 // Possible null reference argument.

            new ManualResetEvent(false).WaitOne();
        }

        private static void SetupIndefiniteKeepAwake(bool displayOn)
        {
            // Indefinite keep awake.
            bool success = APIHelper.SetIndefiniteKeepAwake(displayOn);
            if (success)
            {
                log.Info($"Currently in indefinite keep awake. Display always on: {displayOn}");
            }
            else
            {
                var errorMessage = "Could not set up the state to be indefinite keep awake.";
                log.Info(errorMessage);
                log.Debug(errorMessage);
            }
        }

        private static void HandleEspressoConfigChange(FileSystemEventArgs fileEvent)
        {
            log.Info("Detected a settings file change. Updating configuration...");
            log.Info("Resetting keep-awake to normal state due to settings change.");
            ResetNormalPowerState();
            ProcessSettings();
        }

        private static void ProcessSettings()
        {
            try
            {
                EspressoSettings settings = settingsUtils.GetSettings<EspressoSettings>(appName);

                if (settings != null)
                {
                    // If the settings were successfully processed, we need to set the right mode of operation.
                    // INDEFINITE = 0
                    // TIMED = 1
                    switch (settings.Properties.Mode)
                    {
                        case EspressoMode.INDEFINITE:
                            {
                                // Indefinite keep awake.
                                SetupIndefiniteKeepAwake(settings.Properties.KeepDisplayOn.Value);
                                break;
                            }
                        case EspressoMode.TIMED:
                            {
                                // Timed keep-awake.
                                long computedTime = (settings.Properties.Hours.Value * 60 * 60) + (settings.Properties.Minutes.Value * 60);
                                SetupTimedKeepAwake(computedTime, settings.Properties.KeepDisplayOn.Value);

                                break;
                            }
                        default:
                            {
                                var errorMessage = "Unknown mode of operation. Check config file.";
                                log.Info(errorMessage);
                                log.Debug(errorMessage);
                                break;
                            }
                    }

                    TrayHelper.SetTray(appName, settings);
                }
                else
                {
                    var errorMessage = "Settings are null.";
                    log.Info(errorMessage);
                    log.Debug(errorMessage);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"There was a problem reading the configuration file. Error: {ex.Message}";
                log.Info(errorMessage);
                log.Debug(errorMessage);
            }
        }

        private static void SetupTimedKeepAwake(long time, bool displayOn)
        {
            log.Info($"Timed keep-awake. Expected runtime: {time} seconds.");

            APIHelper.SetTimedKeepAwake(time, LogTimedKeepAwakeCompletion, LogUnexpectedOrCancelledKeepAwakeCompletion, displayOn);
        }

        private static void LogUnexpectedOrCancelledKeepAwakeCompletion()
        {
            var errorMessage = "The keep-awake thread was terminated early.";
            log.Info(errorMessage);
            log.Debug(errorMessage);
        }

        private static void LogTimedKeepAwakeCompletion(bool result)
        {
            log.Info($"Completed timed keep-awake successfully: {result}");
        }

        private static void ResetNormalPowerState()
        {
            bool success = APIHelper.SetNormalKeepAwake();
            if (success)
            {
                log.Info("Returned to normal keep-awake state.");
            }
            else
            {
                var errorMessage = "Could not return to normal keep-awake state.";
                log.Info(errorMessage);
                log.Debug(errorMessage);
            }
        }
    }
}
