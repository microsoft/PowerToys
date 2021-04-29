// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Espresso.Shell.Core;
using Espresso.Shell.Models;
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

namespace Espresso.Shell
{
    class Program
    {
        private static Mutex mutex = null;
        private const string appName = "Espresso";
        private static FileSystemWatcher watcher = null;
        public static Mutex Mutex { get => mutex; set => mutex = value; }

        private static Logger log;

        static int Main(string[] args)
        {
            bool instantiated;
            Mutex = new Mutex(true, appName, out instantiated);

            if (!instantiated)
            {
                ForceExit(appName + " is already running! Exiting the application.", 1);
            }

            log = LogManager.GetCurrentClassLogger();
            
            log.Info("Launching Espresso...");
            log.Info(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
            log.Debug($"OS: {Environment.OSVersion}");
            log.Debug($"OS Build: {APIHelper.GetOperatingSystemBuild()}");

            var configOption = new Option<string>(
                    aliases: new[] { "--config", "-c" },
                    getDefaultValue: () => string.Empty,
                    description: "Pointer to a PowerToys configuration file that the tool will be watching for changes. All other options are disregarded if config is used.")
            {
                Argument = new Argument<string>(() => string.Empty)
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

            var rootCommand = new RootCommand
            {
                configOption,
                displayOption,
                timeOption
            };

            rootCommand.Description = appName;

            rootCommand.Handler = CommandHandler.Create<string, bool, long>(HandleCommandLineArguments);

            return rootCommand.InvokeAsync(args).Result;
        }

        private static void ForceExit(string message, int exitCode)
        {
            log.Debug(message);
            log.Info(message);
            Console.ReadKey();
            Environment.Exit(exitCode);
        }

        private static void HandleCommandLineArguments(string config, bool displayOn, long timeLimit)
        {
            log.Info($"The value for --config is: {config}");
            log.Info($"The value for --display-on is: {displayOn}");
            log.Info($"The value for --time-limit is: {timeLimit}");

            if (!string.IsNullOrWhiteSpace(config))
            {
                // Configuration file is used, therefore we disregard any other command-line parameter
                // and instead watch for changes in the file.
                try
                {
                    watcher = new FileSystemWatcher
                    {
                        Path = Path.GetDirectoryName(config),
                        EnableRaisingEvents = true,
                        NotifyFilter = NotifyFilters.LastWrite,
                        Filter = Path.GetFileName(config)
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
                    ProcessSettings(config);
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
                else
                {
                    // Timed keep-awake.
                    APIHelper.SetTimedKeepAwake(timeLimit, LogTimedKeepAwakeCompletion, LogUnexpectedOrCancelledKeepAwakeCompletion, displayOn);
                }
            }

            new ManualResetEvent(false).WaitOne();
        }

        private static void HandleEspressoConfigChange(FileSystemEventArgs fileEvent)
        {
            log.Info("Detected a settings file change. Updating configuration...");
            log.Info("Resetting keep-awake to normal state due to settings change.");
            ResetNormalPowerState();
            ProcessSettings(fileEvent.FullPath);
        }

        private static void ProcessSettings(string fullPath)
        {
            try
            {
                EspressoSettingsModel settings = null;

                var fileStream = SettingsHelper.GetSettingsFile(fullPath, 3);
                if (fileStream != null)
                {
                    using (fileStream)
                    {
                        using StreamReader reader = new StreamReader(fileStream);
                        settings = JsonConvert.DeserializeObject<EspressoSettingsModel>(reader.ReadToEnd());
                    }

                    if (settings != null)
                    {
                        // If the settings were successfully processed, we need to set the right mode of operation.
                        // INDEFINITE = 0
                        // TIMED = 1
                        switch (settings.Properties.Mode)
                        {
                            case 0:
                                {
                                    // Indefinite keep awake.
                                    bool success = APIHelper.SetIndefiniteKeepAwake(settings.Properties.KeepDisplayOn.Value);
                                    if (success)
                                    {
                                        log.Info($"Indefinite keep-awake. Display always on: {settings.Properties.KeepDisplayOn.Value}");
                                    }
                                    else
                                    {
                                        var errorMessage = "Could not set up the state to be indefinite keep-awake.";
                                        log.Info(errorMessage);
                                        log.Debug(errorMessage);
                                    }
                                    break;
                                }
                            case 1:
                                {
                                    // Timed keep-awake.
                                    long computedTime = (settings.Properties.Hours.Value * 60 * 60) + (settings.Properties.Minutes.Value * 60);
                                    log.Info($"Timed keep-awake. Expected runtime: {computedTime} seconds.");

                                    APIHelper.SetTimedKeepAwake(computedTime, LogTimedKeepAwakeCompletion, LogUnexpectedOrCancelledKeepAwakeCompletion, settings.Properties.KeepDisplayOn.Value);

                                    break;
                                }
                            default:
                                {
                                    var errorMessage= "Unknown mode of operation. Check config file.";
                                    log.Info(errorMessage);
                                    log.Debug(errorMessage);
                                    break;
                                }
                        }
                    }
                    else
                    {
                        var errorMessage = "Settings are null.";
                        log.Info(errorMessage);
                        log.Debug(errorMessage);
                    }
                }
                else
                {
                    var errorMessage = "Could not get handle on file.";
                    log.Info(errorMessage);
                    log.Debug(errorMessage);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"There was a problem reading the configuration file. Error: {ex.Message}";
                log.Info(errorMessage);
                log.Debug(errorMessage);
                log.Debug($"Configuration path: {fullPath}");
            }
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
