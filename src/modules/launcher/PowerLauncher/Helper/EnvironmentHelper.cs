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
        // The HashSet will contain the list of environment variables that will be skipped on update.
        private const string PathVariable = "PATH";
        private static HashSet<string> protectedProcessVariables = new HashSet<string>();

        /// <summary>
        /// This method is called from <see cref="MainWindow.OnSourceInitialized"/> to initialize a list of protected environment variables after process initialization.
        /// Protected variables are environment variables that must not be changed on process level when updating the environment variables with changes on machine and/or user level.
        /// This method is used to fill the private variable <see cref="protectedProcessVariables"/>.
        /// </summary>
        internal static void GetProtectedEnvironmentVariables()
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
                GetMachineAndUserVariables(machineAndUserVars);

                // Adding names of variables that are different on process level or existing only on process level
                foreach (KeyValuePair<string, string> pVar in processVars)
                {
                    if (machineAndUserVars.ContainsKey(pVar.Key))
                    {
                        if (machineAndUserVars[pVar.Key] != pVar.Value)
                        {
                            // Variable value for this process differs form merged machine/user value.
                            protectedProcessVariables.Add(pVar.Key);
                        }
                    }
                    else
                    {
                        // Variable exists only for this process
                        protectedProcessVariables.Add(pVar.Key);
                    }
                }
            });
        }

        /// <summary>
        /// This method updates the environment of PT Run's process when called. It is called when we receive a special WindowMessage.
        /// </summary>
        internal static void UpdateEnvironment()
        {
            var newEnvironment = new Dictionary<string, string>();
            GetMachineAndUserVariables(newEnvironment);
            MarkRemovedVariablesForDeletion(newEnvironment);

            foreach (KeyValuePair<string, string> kv in newEnvironment)
            {
                // Initialize variables for length of environment variable name and value. Using this variables prevent us from null value exceptions.
                int varNameLength = kv.Key == null ? 0 : kv.Key.Length;
                int varValueLength = kv.Value == null ? 0 : kv.Value.Length;

                // The name of environment variables must not be null, empty or have a length of zero.
                // But if the value of the environment variable is null or an empty string then the variable is explicit defined for deletion. => Here we don't need to check anything.
                if (!string.IsNullOrEmpty(kv.Key) & varNameLength > 0)
                {
                    try
                    {
                        if (!protectedProcessVariables.Contains(kv.Key))
                        {
                            /// If the variable is not listed as protected/don't override on process level, then update it (<see cref="GetProtectedEnvironmentVariables"/>).
                            Environment.SetEnvironmentVariable(kv.Key, kv.Value, EnvironmentVariableTarget.Process);
                        }
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
        /// This method gets all deleted environment variables and adds them to a dictionary.
        /// To delete variables with <see cref="Environment.SetEnvironmentVariable(string, string?)"/> the second parameter (value) must be null or an empty string (<see href="https://docs.microsoft.com/en-us/dotnet/api/system.environment.setenvironmentvariable"/>).
        /// </summary>
        /// <param name="environment">The dictionary of variable on which the deleted variables should be listed/added.</param>
        private static void MarkRemovedVariablesForDeletion(Dictionary<string, string> environment)
        {
            IDictionary processVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process);

            foreach (KeyValuePair<string, string> pVar in processVars)
            {
                if (!environment.ContainsKey(pVar.Key))
                {
                    environment.Add(pVar.Key, string.Empty);
                }
            }
        }

        /// <summary>
        /// This method returns a Dictionary with a merged set of machine and user environment variables. If we run as "system" only machine variables are returned.
        /// </summary>
        /// <param name="environment">The dictionary that should be filled with the merged variables.</param>
        private static void GetMachineAndUserVariables(Dictionary<string, string> environment)
        {
            // Getting machine variables
            IDictionary machineVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
            foreach (KeyValuePair<string, string> mVar in machineVars)
            {
                environment[mVar.Key] = mVar.Value;
            }

            // Getting user variables and merge it
            if (!IsRunningAsSystem())
            {
                IDictionary userVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User);
                foreach (KeyValuePair<string, string> uVar in userVars)
                {
                    if (uVar.Key != PathVariable)
                    {
                        environment[uVar.Key] = uVar.Value;
                    }
                    else
                    {
                        // When we merging the PATH variable we can't simply override machine layer's value. The path variable must be joined by appending the user value to the machine value.
                        // This is the official behavior and checked by trying it out the physical machine.
                        string newPathValue = environment[PathVariable].EndsWith(';') ? environment[PathVariable] + uVar.Value : environment[PathVariable] + ';' + uVar.Value;
                        environment[PathVariable] = newPathValue;
                    }
                }
            }
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
