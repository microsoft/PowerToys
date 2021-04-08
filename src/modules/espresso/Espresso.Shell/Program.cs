using Espresso.Shell.Core;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading;

namespace Espresso.Shell
{
    class Program
    {
        private static Mutex mutex = null;
        private const string appName = "Espresso";

        static int Main(string[] args)
        {
            bool instantiated;
            mutex = new Mutex(true, appName, out instantiated);

            if (!instantiated)
            {
                Console.WriteLine(appName + " is already running! Exiting the application.");
                Console.ReadKey();
                Environment.Exit(1);
            }

            Console.WriteLine("Espresso - Computer Caffeination Engine");

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
                displayOption,
                timeOption
            };

            rootCommand.Description = appName;

            rootCommand.Handler = CommandHandler.Create<bool, long>(HandleCommandLineArguments);

            return rootCommand.InvokeAsync(args).Result;
        }

        private static void HandleCommandLineArguments(bool displayOn, long timeLimit)
        {
            Console.WriteLine($"The value for --display-on is: {displayOn}");
            Console.WriteLine($"The value for --time-limit is: {timeLimit}");
            
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
                bool success = APIHelper.SetTimedKeepAwake(timeLimit, displayOn);
                if (success)
                {
                    Console.WriteLine($"Finished execution of timed keep-awake.");

                    // Because the timed keep-awake execution completed, there is no reason for
                    // Espresso to stay alive - I will just shut down the application until it's
                    // launched again by the user.
                    Environment.Exit(0);
                }
                else
                {
                    Console.WriteLine("Could not set up the state to be timed keep awake.");
                }
            }

            new ManualResetEvent(false).WaitOne();
        }
    }
}
