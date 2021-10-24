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
        // <Note>
        // On Windows operating system the name of environment variables is case insensitive. This means if we have a user and machine variable with differences in their name casing (eg. test vs Test), the name casing from machine level is used and won't be overwritten by the user var.
        // Example for Window's behavior: test=ValueMachine (Machine level) + TEST=ValueUser (User level) => test=ValueUser (merged)
        // To get the same behavior we use the "StringComparer.OrdinalIgnoreCase" for the HashSet and Dictionaries where we merge machine and user variable names.
        // </Note>

        // The HashSet will contain the list of environment variables that will be skipped on update.
        private const string PathVariable = "Path";
        private static HashSet<string> protectedProcessVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// This method is called from <see cref="MainWindow.OnSourceInitialized"/> to initialize a list of protected environment variables after process initialization.
        /// Protected variables are environment variables that must not be changed on process level when updating the environment variables with changes on machine and/or user level.
        /// This method is used to fill the private variable <see cref="protectedProcessVariables"/>.
        /// </summary>
        internal static void GetProtectedEnvironmentVariables()
        {
            IDictionary processVars;
            var machineAndUserVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Stopwatch.Normal("EnvironmentHelper.GetProtectedEnvironmentVariables - Duration cost", () =>
            {
                // Adding some well known variables that must kept unchanged on process level.
                // Changes of this variables may lead to incorrect values
                protectedProcessVariables.Add("USERNAME");
                protectedProcessVariables.Add("PROCESSOR_ARCHITECTURE");

                // Getting environment variables
                processVars = GetEnvironmentVariablesWithErrorLog(EnvironmentVariableTarget.Process);
                GetMergedMachineAndUserVariables(machineAndUserVars);

                // Adding names of variables that are different on process level or existing only on process level
                foreach (DictionaryEntry pVar in processVars)
                {
                    string pVarKey = (string)pVar.Key;
                    string pVarValue = (string)pVar.Value;

                    if (machineAndUserVars.ContainsKey(pVarKey))
                    {
                        if (machineAndUserVars[pVarKey] != pVarValue)
                        {
                            // Variable value for this process differs form merged machine/user value.
                            protectedProcessVariables.Add(pVarKey);
                        }
                    }
                    else
                    {
                        // Variable exists only for this process
                        protectedProcessVariables.Add(pVarKey);
                    }
                }
            });
        }

        /// <summary>
        /// This method updates the environment of PT Run's process when called. It is called when we receive a special WindowMessage.
        /// </summary>
        internal static void UpdateEnvironment()
        {
            Stopwatch.Normal("EnvironmentHelper.UpdateEnvironment - Duration cost", () =>
            {
                // Getting updated environment variables
                var newEnvironment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                GetMergedMachineAndUserVariables(newEnvironment);
                GetDeletedMachineAndUserVariables(newEnvironment);

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
                            /// If the variable is not listed as protected/don't override on process level, then update it (<see cref="GetProtectedEnvironmentVariables"/>).
                            if (!protectedProcessVariables.Contains(kv.Key))
                            {
                                /// <summary>
                                /// We have to delete the variables first that we can update the casing of the variable name too.
                                /// The variables that we have to delete are marked with a null value in <see cref="kv.Value"/>. We check the values against null or empty string that we don't try to delete a not existing variable.
                                /// The dotnet method doesn't throw an exception if the deleted variable doesn't exist.
                                /// </summary>
                                Environment.SetEnvironmentVariable(kv.Key, null, EnvironmentVariableTarget.Process);
                                if (!string.IsNullOrEmpty(kv.Value))
                                {
                                    Environment.SetEnvironmentVariable(kv.Key, kv.Value, EnvironmentVariableTarget.Process);
                                }
                            }
                        }
#pragma warning disable CA1031 // Do not catch general exception types
                        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                        {
                            // The dotnet method <see cref="System.Environment.SetEnvironmentVariable"/> has it's own internal method to check the input parameters. Here we catch the exceptions that we don't check before updating the environment variable and log it to avoid crashes of PT Run.
                            Log.Exception($"Unhandled exception while updating the environment variable [{kv.Key}] for the PT Run process. (The variable value has a length of [{varValueLength}].)", ex, typeof(PowerLauncher.Helper.EnvironmentHelper));
                        }
                    }
                    else
                    {
                        // Log the error when variable value is null, empty or has a length of zero.
                        Log.Error($"Failed to update the environment variable [{kv.Key}] for the PT Run process. Their name is null or empty. (The variable value has a length of [{varValueLength}].)", typeof(PowerLauncher.Helper.EnvironmentHelper));
                    }
                }
            });
        }

        /// <summary>
        /// This method gets all deleted environment variables and adds them to a dictionary.
        /// To delete variables with <see cref="Environment.SetEnvironmentVariable(string, string?)"/> the second parameter (value) must be null or an empty string (<see href="https://docs.microsoft.com/en-us/dotnet/api/system.environment.setenvironmentvariable"/>).
        /// </summary>
        /// <param name="environment">The dictionary of variable on which the deleted variables should be listed/added.</param>
        private static void GetDeletedMachineAndUserVariables(Dictionary<string, string> environment)
        {
            IDictionary processVars = GetEnvironmentVariablesWithErrorLog(EnvironmentVariableTarget.Process);

            foreach (DictionaryEntry pVar in processVars)
            {
                string pVarKey = (string)pVar.Key;

                if (!environment.ContainsKey((string)pVarKey))
                {
                    environment.Add((string)pVarKey, string.Empty);
                }
            }
        }

        /// <summary>
        /// This method returns a Dictionary with a merged set of machine and user environment variables. If we run as "system" only machine variables are returned.
        /// </summary>
        /// <param name="environment">The dictionary that should be filled with the merged variables.</param>
        private static void GetMergedMachineAndUserVariables(Dictionary<string, string> environment)
        {
            // Getting machine variables
            IDictionary machineVars = GetEnvironmentVariablesWithErrorLog(EnvironmentVariableTarget.Machine);
            foreach (DictionaryEntry mVar in machineVars)
            {
                environment[(string)mVar.Key] = (string)mVar.Value;
            }

            // Getting user variables and merge it
            if (!IsRunningAsSystem())
            {
                IDictionary userVars = GetEnvironmentVariablesWithErrorLog(EnvironmentVariableTarget.User);
                foreach (DictionaryEntry uVar in userVars)
                {
                    string uVarKey = (string)uVar.Key;
                    string uVarValue = (string)uVar.Value;

                    // The variable name of the path variable can be upper case, lower case ore mixed case. So we have to compare case insensitive.
                    if (!uVarKey.Equals(PathVariable, StringComparison.OrdinalIgnoreCase))
                    {
                        environment[uVarKey] = uVarValue;
                    }
                    else
                    {
                        // When we merging the PATH variables we can't simply overwrite machine layer's value. The path variable must be joined by appending the user value to the machine value.
                        // This is the official behavior and checked by trying it out on the physical machine.
                        string newPathValue = environment[uVarKey].EndsWith(';') ? environment[uVarKey] + uVarValue : environment[uVarKey] + ';' + uVarValue;
                        environment[uVarKey] = newPathValue;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the variables for the specified target. Errors taht occurs will be catched and logged.
        /// </summary>
        /// <param name="target">The target variable source of the type <see cref="EnvironmentVariableTarget"/> </param>
        /// <returns>A dictionary with the variable or an empty dictionary on errors.</returns>
        private static IDictionary GetEnvironmentVariablesWithErrorLog(EnvironmentVariableTarget target)
        {
            try
            {
                return Environment.GetEnvironmentVariables(target);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log.Exception($"Unhandled exception while getting the environment variables for target '{target}'.", ex, typeof(PowerLauncher.Helper.EnvironmentHelper));
                return new Hashtable();
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
