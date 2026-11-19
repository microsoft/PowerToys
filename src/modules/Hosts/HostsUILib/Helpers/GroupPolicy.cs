// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security;

using Microsoft.Win32;

namespace HostsUILib.Helpers
{
    /// <summary>
    /// Reads the PowerToys Group Policy that controls whether the Hosts File Editor is enabled.
    /// Mirrors the native powertoys_gpo::getUtilityEnabledValue logic so the library enforces
    /// the policy in any host, without a dependency on the native GPO wrapper.
    /// </summary>
    internal static class GroupPolicy
    {
        internal const string PoliciesPath = @"SOFTWARE\Policies\PowerToys";
        internal const string HostsFileEditorEnabledValueName = "ConfigureEnabledUtilityHostsFileEditor";
        internal const string GlobalUtilityEnabledValueName = "ConfigureGlobalUtilityEnabledState";

        private const int PolicyDisabled = 0;
        private const int PolicyEnabled = 1;

        public static bool IsHostsFileEditorDisabled() => IsHostsFileEditorDisabled(ReadPolicyValue);

        /// <param name="readPolicyValue">Returns the configured DWORD for a policy value name, or null when it isn't set.</param>
        internal static bool IsHostsFileEditorDisabled(Func<string, int?> readPolicyValue)
        {
            var value = readPolicyValue(HostsFileEditorEnabledValueName);
            if (value is not (PolicyDisabled or PolicyEnabled))
            {
                value = readPolicyValue(GlobalUtilityEnabledValueName);
            }

            return value == PolicyDisabled;
        }

        // Machine scope wins over user scope, as in the native reader.
        private static int? ReadPolicyValue(string valueName)
        {
            return ReadDword(Registry.LocalMachine, valueName) ?? ReadDword(Registry.CurrentUser, valueName);
        }

        private static int? ReadDword(RegistryKey root, string valueName)
        {
            try
            {
                using var key = root.OpenSubKey(PoliciesPath);
                return key?.GetValue(valueName) is int value ? value : null;
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
            {
                return null;
            }
        }
    }
}
