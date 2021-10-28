// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Wox.Plugin.Logger;

namespace PowerLauncher.Helper
{
    public static class EnvironmentHelper
    {
        private const string Username = "USERNAME";
        private const string ProcessorArchitecture = "PROCESSOR_ARCHITECTURE";
        private const string Path = "PATH";

        internal static void UpdateEnvironment()
        {
            // Username and process architecture are set by the machine vars, this
            // may lead to incorrect values so save off the current values to restore.
            string originalUsername = Environment.GetEnvironmentVariable(Username, EnvironmentVariableTarget.Process);
            string originalArch = Environment.GetEnvironmentVariable(ProcessorArchitecture, EnvironmentVariableTarget.Process);

            var environment = new Dictionary<string, string>();
            MergeTargetEnvironmentVariables(environment, EnvironmentVariableTarget.Process);
            MergeTargetEnvironmentVariables(environment, EnvironmentVariableTarget.Machine);

            if (!IsRunningAsSystem())
            {
                MergeTargetEnvironmentVariables(environment, EnvironmentVariableTarget.User);

                // Special handling for PATH - merge Machine & User instead of override
                var pathTargets = new[] { EnvironmentVariableTarget.Machine, EnvironmentVariableTarget.User };
                var paths = pathTargets
                    .Select(t => Environment.GetEnvironmentVariable(Path, t))
                    .Where(e => e != null)
                    .SelectMany(e => e.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    .Distinct();

                environment[Path] = string.Join(';', paths);
            }

            environment[Username] = originalUsername;
            environment[ProcessorArchitecture] = originalArch;

            foreach (KeyValuePair<string, string> kv in environment)
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

        private static void MergeTargetEnvironmentVariables(
            Dictionary<string, string> environment, EnvironmentVariableTarget target)
        {
            IDictionary variables = Environment.GetEnvironmentVariables(target);
            foreach (DictionaryEntry entry in variables)
            {
                environment[(string)entry.Key] = (string)entry.Value;
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
