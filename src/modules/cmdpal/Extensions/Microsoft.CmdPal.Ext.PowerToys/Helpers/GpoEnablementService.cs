// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Win32;

namespace PowerToysExtension.Helpers;

internal enum GpoRuleConfiguredValue
{
    WrongValue = -3,
    Unavailable = -2,
    NotConfigured = -1,
    Disabled = 0,
    Enabled = 1,
}

/// <summary>
/// Lightweight GPO reader for module/feature enablement policies.
/// Mirrors the logic in src/common/utils/gpo.h but avoids taking a dependency on the full GPOWrapper.
/// </summary>
internal static class GpoEnablementService
{
    private const string PoliciesPath = @"SOFTWARE\Policies\PowerToys";
    private const string PolicyConfigureEnabledGlobalAllUtilities = "ConfigureGlobalUtilityEnabledState";

    internal static GpoRuleConfiguredValue GetUtilityEnabledValue(string individualPolicyValueName)
    {
        if (!string.IsNullOrEmpty(individualPolicyValueName))
        {
            var individual = GetConfiguredValue(individualPolicyValueName);
            if (individual is GpoRuleConfiguredValue.Disabled or GpoRuleConfiguredValue.Enabled)
            {
                return individual;
            }
        }

        return GetConfiguredValue(PolicyConfigureEnabledGlobalAllUtilities);
    }

    private static GpoRuleConfiguredValue GetConfiguredValue(string registryValueName)
    {
        try
        {
            // Machine scope has priority over user scope.
            var value = ReadRegistryValue(Registry.LocalMachine, registryValueName);
            value ??= ReadRegistryValue(Registry.CurrentUser, registryValueName);

            if (!value.HasValue)
            {
                return GpoRuleConfiguredValue.NotConfigured;
            }

            return value.Value switch
            {
                0 => GpoRuleConfiguredValue.Disabled,
                1 => GpoRuleConfiguredValue.Enabled,
                _ => GpoRuleConfiguredValue.WrongValue,
            };
        }
        catch
        {
            return GpoRuleConfiguredValue.Unavailable;
        }
    }

    private static int? ReadRegistryValue(RegistryKey rootKey, string valueName)
    {
        try
        {
            using var key = rootKey.OpenSubKey(PoliciesPath, writable: false);
            if (key is null)
            {
                return null;
            }

            var value = key.GetValue(valueName);
            return value as int?;
        }
        catch
        {
            return null;
        }
    }
}
