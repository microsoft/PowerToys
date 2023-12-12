// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using Wox.Plugin.Logger;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace PowerLauncher.Helper
{
    /// <Note>
    /// On Windows operating system the name of environment variables is case insensitive. This means if we have a user and machine variable with differences in their name casing (eg. test vs Test), the name casing from machine level is used and won't be overwritten by the user var.
    /// Example for Window's behavior: test=ValueMachine (Machine level) + TEST=ValueUser (User level) => test=ValueUser (merged)
    /// To get the same behavior we use "StringComparer.OrdinalIgnoreCase" as compare property for the HashSet and Dictionaries where we merge machine and user variable names.
    /// </Note>
    public static class EnvironmentHelper
    {
        // The HashSet will contain the list of environment variables that will be skipped on update.
        private const string PathVariableName = "Path";
        private static readonly HashSet<string> _protectedProcessVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// This method is called from <see cref="MainWindow.OnSourceInitialized"/> to initialize a list of protected environment variables right after the PT Run process has been invoked.
        /// Protected variables are environment variables that must not be changed on process level when updating the environment variables with changes on machine and/or user level.
        /// We cache the relevant variable names in the private, static and readonly variable <see cref="_protectedProcessVariables"/> of this class.
        /// </summary>
        internal static void GetProtectedEnvironmentVariables()
        {
            IDictionary processVars;
            var machineAndUserVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Stopwatch.Normal("EnvironmentHelper.GetProtectedEnvironmentVariables - Duration cost", () =>
            {
                // Adding some well known variables that must kept unchanged on process level.
                // Changes of this variables may lead to incorrect values
                _protectedProcessVariables.Add("USERNAME");
                _protectedProcessVariables.Add("PROCESSOR_ARCHITECTURE");

                // Getting environment variables
                processVars = GetEnvironmentVariablesWithErrorLog(EnvironmentVariableTarget.Process);
                GetMergedMachineAndUserVariables(machineAndUserVars);

                // Adding names of variables that are different on process level or existing only on process level
                foreach (DictionaryEntry pVar in processVars)
                {
                    string pVarKey = (string)pVar.Key;
                    string pVarValue = (string)pVar.Value;

                    if (machineAndUserVars.TryGetValue(pVarKey, out string value))
                    {
                        if (value != pVarValue)
                        {
                            // Variable value for this process differs form merged machine/user value.
                            _protectedProcessVariables.Add(pVarKey);
                        }
                    }
                    else
                    {
                        // Variable exists only for this process
                        _protectedProcessVariables.Add(pVarKey);
                    }
                }
            });
        }

        /// <summary>
        /// This method is used as a function wrapper to do the update twice. It is called when we receive a special WindowMessage.
        /// </summary>
        internal static void UpdateEnvironment()
        {
            Stopwatch.Normal("EnvironmentHelper.UpdateEnvironment - Duration cost", () =>
            {
                // We have to do the update twice to get a correct variable set, if some variables reference other variables in their value (e.g. PATH contains %JAVA_HOME%). [https://github.com/microsoft/PowerToys/issues/26864]
                // The cause of this is a bug in .Net which reads the variables from the Registry (HKLM/HKCU), but expands the REG_EXPAND_SZ values against the current process environment when reading the Registry value.
                ExecuteEnvironmentUpdate();
                ExecuteEnvironmentUpdate();
            });
        }

        /// <summary>
        /// This method updates the environment of PT Run's process when called.
        /// </summary>
        private static void ExecuteEnvironmentUpdate()
        {
                // Caching existing process environment and getting updated environment variables
                IDictionary oldProcessEnvironment = GetEnvironmentVariablesWithErrorLog(EnvironmentVariableTarget.Process);
                var newEnvironment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                GetMergedMachineAndUserVariables(newEnvironment);

                // Determine deleted variables and add them with a "string.Empty" value as marker to the dictionary
                foreach (DictionaryEntry pVar in oldProcessEnvironment)
                {
                    // We must compare case insensitive (see dictionary assignment) to avoid false positives when the variable name has changed (Example: "path" -> "Path")
                    if (!newEnvironment.ContainsKey((string)pVar.Key) & !_protectedProcessVariables.Contains((string)pVar.Key))
                    {
                        newEnvironment.Add((string)pVar.Key, string.Empty);
                    }
                }

                // Remove unchanged variables from the dictionary
                // Later we only like to recreate the changed ones
                foreach (string varName in newEnvironment.Keys.ToList())
                {
                    // To be able to detect changed names correctly we have to compare case sensitive
                    if (oldProcessEnvironment.Contains(varName))
                    {
                        if (oldProcessEnvironment[varName].Equals(newEnvironment[varName]))
                        {
                            newEnvironment.Remove(varName);
                        }
                    }
                }

                // Update PT Run's process environment now
                foreach (KeyValuePair<string, string> kv in newEnvironment)
                {
                    // Initialize variables for length of environment variable name and value. Using this variables prevent us from null value exceptions.
                    // => We added this because of the issue #13172 where a user reported System.ArgumentNullException from "Environment.SetEnvironmentVariable()".
                    int varNameLength = kv.Key == null ? 0 : kv.Key.Length;
                    int varValueLength = kv.Value == null ? 0 : kv.Value.Length;

                    // The name of environment variables must not be null, empty or have a length of zero.
                    // But if the value of the environment variable is null or an empty string then the variable is explicit defined for deletion. => Here we don't need to check anything.
                    // => We added the if statement (next line) because of the issue #13172 where a user reported System.ArgumentNullException from "Environment.SetEnvironmentVariable()".
                    if (!string.IsNullOrEmpty(kv.Key) & varNameLength > 0)
                    {
                        try
                        {
                            // If the variable is not listed as protected/don't override on process level, then update it. (See method "GetProtectedEnvironmentVariables" of this class.)
                            if (!_protectedProcessVariables.Contains(kv.Key))
                            {
                                // We have to delete the variables first that we can update their name if changed by the user. (Example: "path" => "Path")
                                // The machine and user variables that have been deleted by the user having an empty string as variable value. Because of this we check the values of the variables in our dictionary against "null" and "string.Empty". This check prevents us from invoking a (second) delete command.
                                // The dotnet method doesn't throw an exception if the variable which should be deleted doesn't exist.
                                Environment.SetEnvironmentVariable(kv.Key, null, EnvironmentVariableTarget.Process);
                                if (!string.IsNullOrEmpty(kv.Value))
                                {
                                    Environment.SetEnvironmentVariable(kv.Key, kv.Value, EnvironmentVariableTarget.Process);
                                }
                            }
                            else
                            {
                                // Don't log for the variable "USERNAME" if the variable's value is "System". (Then it is a false positive because per default the variable only exists on machine level with the value "System".)
                                if (!kv.Key.Equals("USERNAME", StringComparison.OrdinalIgnoreCase) & !kv.Value.Equals("System", StringComparison.Ordinal))
                                {
                                    Log.Warn($"Skipping update of the environment variable [{kv.Key}] for the PT Run process. This variable is listed as protected process variable and changing them can cause unexpected behavior. (The variable value has a length of [{varValueLength}].)", typeof(PowerLauncher.Helper.EnvironmentHelper));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // The dotnet method "System.Environment.SetEnvironmentVariable" has it's own internal method to check the input parameters. Here we catch the exceptions that we don't check before updating the environment variable and log it to avoid crashes of PT Run.
                            Log.Exception($"Unhandled exception while updating the environment variable [{kv.Key}] for the PT Run process. (The variable value has a length of [{varValueLength}].)", ex, typeof(PowerLauncher.Helper.EnvironmentHelper));
                        }
                    }
                    else
                    {
                        // Log the error when variable name is null, empty or has a length of zero.
                        Log.Error($"Failed to update the environment variable [{kv.Key}] for the PT Run process. Their name is null or empty. (The variable value has a length of [{varValueLength}].)", typeof(PowerLauncher.Helper.EnvironmentHelper));
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
                    if (!uVarKey.Equals(PathVariableName, StringComparison.OrdinalIgnoreCase))
                    {
                        environment[uVarKey] = uVarValue;
                    }
                    else
                    {
                        // Checking if the list of (machine) variables contains a path variable
                        if (environment.ContainsKey(PathVariableName))
                        {
                            // When we merging the PATH variables we can't simply overwrite machine layer's value. The path variable must be joined by appending the user value to the machine value.
                            // This is the official behavior and checked by trying it out on the physical machine.
                            string newPathValue = environment[uVarKey].EndsWith(';') ? environment[uVarKey] + uVarValue : environment[uVarKey] + ';' + uVarValue;
                            environment[uVarKey] = newPathValue;
                        }
                        else
                        {
                            // Log warning and only write user value into dictionary
                            Log.Warn("The List of machine variables doesn't contain a path variable! The merged list won't contain any machine paths in the path variable.", typeof(PowerLauncher.Helper.EnvironmentHelper));
                            environment[uVarKey] = uVarValue;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the variables for the specified target. Errors that occurs will be caught and logged.
        /// </summary>
        /// <param name="target">The target variable source of the type <see cref="EnvironmentVariableTarget"/> </param>
        /// <returns>A dictionary with the variable or an empty dictionary on errors.</returns>
        private static IDictionary GetEnvironmentVariablesWithErrorLog(EnvironmentVariableTarget target)
        {
            try
            {
                return Environment.GetEnvironmentVariables(target);
            }
            catch (Exception ex)
            {
                Log.Exception($"Unhandled exception while getting the environment variables for target '{target}'.", ex, typeof(PowerLauncher.Helper.EnvironmentHelper));
                return new Hashtable();
            }
        }

        /// <summary>
        /// Checks whether this process is running under the system user/account.
        /// </summary>
        /// <returns>A boolean value that indicates whether this process is running under system account (true) or not (false).</returns>
        private static bool IsRunningAsSystem()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                return identity.IsSystem;
            }
        }
    }
}
