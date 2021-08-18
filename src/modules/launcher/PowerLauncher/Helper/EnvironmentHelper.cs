// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Wox.Plugin.Logger;

namespace PowerLauncher.Helper
{
    public static class EnvironmentHelper
    {
        private const string EnvironmentChangeType = "Environment";
        private const string Username = "USERNAME";
        private const string ProcessorArchitecture = "PROCESSOR_ARCHITECTURE";
        private const string Path = "PATH";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1801:Review unused parameters", Justification = "Params are required for delegate signature requirements.")]
        public static IntPtr ProcessWindowMessages(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            switch ((WM)msg)
            {
                case WM.SETTINGCHANGE:
                    string changeType = Marshal.PtrToStringUni(lparam);
                    if (changeType == EnvironmentChangeType)
                    {
                        Log.Info("Reload environment", typeof(EnvironmentHelper));
                        UpdateEnvironment();
                        handled = true;
                    }

                    break;
            }

            return IntPtr.Zero;
        }

        private static void UpdateEnvironment()
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
                Environment.SetEnvironmentVariable(kv.Key, kv.Value, EnvironmentVariableTarget.Process);
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
