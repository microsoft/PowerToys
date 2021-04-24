// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Espresso.Shell.Core;
using Espresso.Shell.Models;
using Newtonsoft.Json;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading;

namespace Espresso.Shell
{
    class Program
    {
        private static Mutex mutex = null;
        private const string appName = "Espresso";
        private static FileSystemWatcher watcher = null;
        public static Mutex Mutex { get => mutex; set => mutex = value; }

        static int Main(string[] args)
        {
            bool instantiated;
            Mutex = new Mutex(true, appName, out instantiated);

            if (!instantiated)
            {
                ForceExit(appName + " is already running! Exiting the application.", 1);
            }

            Console.WriteLine("Espresso - Computer Caffeination Engine");

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
            Console.WriteLine(message);
            Console.ReadKey();
            Environment.Exit(exitCode);
        }

        private static void HandleCommandLineArguments(string config, bool displayOn, long timeLimit)
        {
            Console.WriteLine($"The value for --display-on is: {displayOn}");
            Console.WriteLine($"The value for --time-limit is: {timeLimit}");

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
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                        Filter = Path.GetFileName(config)
                    };
                    watcher.Changed += new FileSystemEventHandler(HandleEspressoConfigChange);

                    // Initially the file might not be updated, so we need to start processing
                    // settings right away.
                    ProcessSettings(config);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"There was a problem with the configuration file. Make sure it exists.\n{ex.Message}");
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
                        Console.WriteLine($"Currently in indefinite keep awake. Display always on: {displayOn}");
                    }
                    else
                    {
                        Console.WriteLine("Could not set up the state to be indefinite keep awake.");
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

        private static void HandleEspressoConfigChange(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("Resetting keep-awake to normal state due to settings change.");
            ResetNormalPowerState();
            Console.WriteLine("Detected a file change. Reacting...");
            ProcessSettings(e.FullPath);
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
                                        Console.WriteLine($"Currently in indefinite keep awake. Display always on: {settings.Properties.KeepDisplayOn.Value}");
                                    }
                                    else
                                    {
                                        Console.WriteLine("Could not set up the state to be indefinite keep awake.");
                                    }
                                    break;
                                }
                            case 1:
                                {
                                    // Timed keep-awake.
                                    long computedTime = (settings.Properties.Hours.Value * 60 * 60) + (settings.Properties.Minutes.Value * 60);
                                    Console.WriteLine($"In timed keep-awake mode. Expecting to be awake for {computedTime} seconds.");

                                    APIHelper.SetTimedKeepAwake(computedTime, LogTimedKeepAwakeCompletion, LogUnexpectedOrCancelledKeepAwakeCompletion, settings.Properties.KeepDisplayOn.Value);

                                    break;
                                }
                            default:
                                {
                                    Console.WriteLine("Unknown mode of operation. Check config file.");
                                    break;
                                }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Settings are null.");
                    }
                }
                else
                {
                    Console.WriteLine("Could not get handle on file.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"There was a problem reading the configuration file.\n{ex.Message}");
            }
        }

        private static void LogUnexpectedOrCancelledKeepAwakeCompletion()
        {
            Console.Write("The keep-awake thread was terminated early.");
        }

        private static void LogTimedKeepAwakeCompletion(bool result)
        {
            Console.Write($"Completed timed keep-awake successfully: {result}");
        }

        private static void ResetNormalPowerState()
        {
            bool success = APIHelper.SetNormalKeepAwake();
            if (success)
            {
                Console.WriteLine("Returned to normal keep-awake state.");
            }
            else
            {
                Console.WriteLine("Could not return to normal keep-awake state.");
            }
        }
    }
}
