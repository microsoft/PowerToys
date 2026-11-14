// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security;

using Microsoft.Win32;

namespace EnvironmentVariablesUILib.Helpers
{
    /// <summary>
    /// Reads the PowerToys Group Policy that controls the Environment Variables editor,
    /// with the same semantics as the native GPO helper (src/common/utils/gpo.h).
    /// </summary>
    public static class EnvironmentVariablesPolicy
    {
        internal const string PoliciesKeyPath = @"SOFTWARE\Policies\PowerToys";
        internal const string UtilityValueName = "ConfigureEnabledUtilityEnvironmentVariables";
        internal const string GlobalValueName = "ConfigureGlobalUtilityEnabledState";

        /// <summary>
        /// Gets a value indicating whether policy disables the editor.
        /// </summary>
        /// <returns><see langword="true"/> when the editor policy, or the global utility policy if the editor policy is not set, is disabled.</returns>
        public static bool IsDisabled() => IsDisabled(ReadPolicyValue);

        internal static bool IsDisabled(Func<RegistryHive, string, object> readValue)
        {
            var configured = GetConfiguredValue(readValue, UtilityValueName) ?? GetConfiguredValue(readValue, GlobalValueName);
            return configured == false;
        }

        // A machine value takes precedence over a user value. Only DWORD 0 or 1 counts as configured.
        private static bool? GetConfiguredValue(Func<RegistryHive, string, object> readValue, string valueName)
        {
            var value = readValue(RegistryHive.LocalMachine, valueName) ?? readValue(RegistryHive.CurrentUser, valueName);
            return value switch
            {
                0 => false,
                1 => true,
                _ => null,
            };
        }

        private static object ReadPolicyValue(RegistryHive hive, string valueName)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = baseKey.OpenSubKey(PoliciesKeyPath);
                return key?.GetValue(valueName);
            }
            catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException)
            {
                return null;
            }
        }
    }
}
