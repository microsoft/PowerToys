// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Versioning;
using Microsoft.Win32;

namespace PowerScripts.Core;

public static class PowerScriptsPolicies
{
    public const string PolicyKey = @"Software\Policies\PowerToys";
    public const string ConsumersKey = PolicyKey + @"\PowerScripts\Consumers";

    public static bool? ReadUtility(string utility, Func<string, string, bool?>? read = null)
    {
        read ??= Read;
        return read(PolicyKey, $"ConfigureEnabledUtility{utility}") ??
            read(PolicyKey, "ConfigureGlobalUtilityEnabledState");
    }

    public static bool? Read(string key, string name)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        return ReadWindowsPolicy(key, name);
    }

    [SupportedOSPlatform("windows")]
    private static bool? ReadWindowsPolicy(string key, string name) =>
        ResolveValue(machine =>
        {
            using var hive = RegistryKey.OpenBaseKey(machine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64);
            using var policy = hive.OpenSubKey(key);
            return policy?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        });

    // A configured machine value is authoritative, including a malformed value: never fall through
    // to a more permissive user policy when an administrator's value cannot be interpreted.
    public static bool? ResolveValue(Func<bool, object?> read)
    {
        var value = read(true) ?? read(false);
        return value switch
        {
            null => null,
            int number when number == 0 => false,
            int number when number == 1 => true,
            _ => throw new InvalidDataException("PowerScripts Group Policy must be a REG_DWORD with value 0 or 1."),
        };
    }
}
