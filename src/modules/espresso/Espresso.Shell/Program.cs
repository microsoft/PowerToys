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


            var rootCommand = new RootCommand
            {
                new Option<bool>(
                    aliases: new string[] {"--display-on", "-d" },
                    getDefaultValue: () => false,
                    description: "Determines whether the display should be kept awake."),
                new Option<long>(
                    aliases: new string[] {"--time-limit", "-t" },
                    getDefaultValue: () => 0,
                    description: "Determines the interval, in seconds, during which the computer is kept awake.")
            };

            rootCommand.Description = appName;

            rootCommand.Handler = CommandHandler.Create<bool, long>(HandleCommandLineArguments);

            return rootCommand.InvokeAsync(args).Result;
        }

        private static void HandleCommandLineArguments(bool displayOption, long timeOption)
        {
            Console.WriteLine($"The value for --display-on is: {displayOption}");
            Console.WriteLine($"The value for --time-limit is: {timeOption}");
            
            if (timeOption <= 0)
            {
                // Indefinite keep awake.
                bool success = APIHelper.SetIndefiniteKeepAwake(displayOption);
                if (success)
                {
                    Console.WriteLine($"Currently in indefinite keep awake. Display always on: {displayOption}");
                }
                else
                {
                    Console.WriteLine("Could not set up the state to be indefinite keep awake.");
                }
            }

            new ManualResetEvent(false).WaitOne();
        }
    }
}
