// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks;
using Microsoft.UI.Xaml.Documents;
using Microsoft.Win32;

namespace Microsoft.CmdPal.UI.Helpers;

internal enum GpoRuleConfiguredValue
{
    WrongValue = -3,
    Unavailable = -2,
    NotConfigured = -1,
    Disabled = 0,
    Enabled = 1,
}

/*
 * Contains methods extracted from PowerToys gpo.h
 * The idea is to keep CmdPal codebase take as little dependences on the PowerToys codebase as possible.
 * Having this class to check GPO being contained in CmdPal means we don't need to depend on GPOWrapper.
 */
internal static class GpoValueChecker
{
    private const string PoliciesPath = @"SOFTWARE\Policies\PowerToys";
    private static readonly RegistryKey PoliciesScopeMachine = Registry.LocalMachine;
    private static readonly RegistryKey PoliciesScopeUser = Registry.CurrentUser;
    private const string PolicyConfigureEnabledCmdPal = @"ConfigureEnabledUtilityCmdPal";
    private const string PolicyConfigureEnabledGlobalAllUtilities = @"ConfigureGlobalUtilityEnabledState";

    private static GpoRuleConfiguredValue GetConfiguredValue(string registryValueName)
    {
        // For GPO policies, machine scope should take precedence over user scope
        var value = ReadRegistryValue(PoliciesScopeMachine, PoliciesPath, registryValueName);

        if (!value.HasValue)
        {
            // If not found in machine scope, check user scope
            value = ReadRegistryValue(PoliciesScopeUser, PoliciesPath, registryValueName);
            if (!value.HasValue)
            {
                return GpoRuleConfiguredValue.NotConfigured;
            }
        }

        return value switch
        {
            0 => GpoRuleConfiguredValue.Disabled,
            1 => GpoRuleConfiguredValue.Enabled,
            _ => GpoRuleConfiguredValue.WrongValue,
        };
    }

    // Reads an integer registry value if it exists.
    private static int? ReadRegistryValue(RegistryKey rootKey, string subKeyPath, string valueName)
    {
        using (RegistryKey? key = rootKey.OpenSubKey(subKeyPath, false))
        {
            if (key == null)
            {
                return null;
            }

            var value = key.GetValue(valueName);
            if (value is int intValue)
            {
                return intValue;
            }

            return null;
        }
    }

    private static GpoRuleConfiguredValue GetUtilityEnabledValue(string utilityName)
    {
        var individualValue = GetConfiguredValue(utilityName);

        if (individualValue == GpoRuleConfiguredValue.Disabled || individualValue == GpoRuleConfiguredValue.Enabled)
        {
            return individualValue;
        }
        else
        {
            // If the individual utility value is not set, check the global all utilities policy value.
            return GetConfiguredValue(PolicyConfigureEnabledGlobalAllUtilities);
        }
    }

    internal static GpoRuleConfiguredValue GetConfiguredCmdPalEnabledValue()
    {
        return GetUtilityEnabledValue(PolicyConfigureEnabledCmdPal);
    }
}
