// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Principal;
using Wox.Plugin.Logger;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace PowerLauncher.Helper
{
    public static class EnvironmentHelper
    {
        private const string PathVariable = "PATH";
        private static HashSet<string> protectedProcessVariables;

        internal static void UpdateEnvironment()
        {
            var newEnvironment = new Dictionary<string, string>();
            // ToDo: method call

            foreach (KeyValuePair<string, string> kv in newEnvironment)
            {
                // Initialize variables for length of environment variable name and value. Using this variables prevent us from null value exceptions.
                int varNameLength = kv.Key == null ? 0 : kv.Key.Length;
                int varValueLength = kv.Value == null ? 0 : kv.Value.Length;

                // The name of environment variables must not be null, empty or have a length of zero.
                // But if the value of the environment variable is null or empty then the variable is explicit defined for deletion. => Here we don't need to check anything.
                if (!string.IsNullOrEmpty(kv.Key) & varNameLength > 0)
                {
                    try
                    {
                        Environment.SetEnvironmentVariable(kv.Key, kv.Value, EnvironmentVariableTarget.Process);
                    }
                    catch (ArgumentException ex)
                    {
                        // The dotnet method <see cref="System.Environment.SetEnvironmentVariable"/> has it's own internal method to check the input parameters. Here we catch the exceptions that we don't check before updating the environment variable and log it to avoid crashes of PT Run.
                        Log.Exception($"Unexpected exception while updating the environment variable [{kv.Key}] for the PT Run process. (The variable value has a length of [{varValueLength}].)", ex, typeof(PowerLauncher.Helper.EnvironmentHelper));
                    }
                }
                else
                {
                    // Log the error when variable value is null, empty or has a length of zero.
                    Log.Error($"Failed to update the environment variable [{kv.Key}] for the PT Run process. Their name is null or empty. (The variable value has a length of [{varValueLength}].)", typeof(PowerLauncher.Helper.EnvironmentHelper));
                }
            }
        }

        /// <summary>
        /// This method returns a Dictionary with a merged set of machine and user environment variables. If we run as "system" only machine variables are returned.
        /// </summary>
        private static void GetMachineAndUserEnvVariables(Dictionary<string, string> environment)
        {
            // Getting machine variables
            IDictionary mV = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
            foreach (DictionaryEntry entry in mV)
            {
                environment[(string)entry.Key] = (string)entry.Value;
            }

            // Getting user variables and merge it
            if (!IsRunningAsSystem())
            {
                IDictionary uV = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
                foreach (DictionaryEntry entry in uV)
                {
                    if (!(entry.Key.ToString() == PathVariable))
                    {
                        environment[(string)entry.Key] = (string)entry.Value;
                    }
                    else
                    {
                        // When we merging the PATH variable we can't simply override machine layer's value. The path variable must be joined by appending the user value to the machine value.
                        // This is the official behavior and checked by trying it out the physical machine.
                        string newPathValue = environment[PathVariable].EndsWith(";", StringComparison.InvariantCulture) ? environment[PathVariable] + (string)entry.Value : environment[PathVariable] + ";" + (string)entry.Value;
                        environment[PathVariable] = newPathValue;
                    }
                }
            }
        }

        /// <summary>
        /// This method is called from <see cref="MainWindow.OnSourceInitialized"/> to initialize a list of protected environment variables after process initialization.
        /// Protected variables are environment variables that must not be changed on process level when updating the environment variables with changes on machine and/or user level.
        /// This method is used to fill the private variable <see cref="protectedProcessVariables"/>.
        /// </summary>
        public static void GetProtectedEnvVariables()
        {
            IDictionary processVars;
            var machineAndUserVars = new Dictionary<string, string>();

            Stopwatch.Normal("EnvironmentHelper.GetProtectedEnvironmentVariables - Duration cost", () =>
            {
                // Adding some well known variables that must kept unchanged on process level.
                // Changes of this variables may lead to incorrect values
                protectedProcessVariables.Add("USERNAME");
                protectedProcessVariables.Add("PROCESSOR_ARCHITECTURE");

                // Getting environment variables
                processVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process);
                GetMachineAndUserEnvVariables(machineAndUserVars);

                // Adding variable names that 
            });
        }

        private static bool IsRunningAsSystem()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                return identity.IsSystem;
            }
        }
    }
}
