// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Security;

/// <summary>The globally resolved MXC enforcement decision for a process launch.</summary>
public sealed class MxcEffectivePolicy
{
    public bool Enabled { get; init; } = true;

    public IReadOnlyList<string> EnabledPolicies { get; init; } = MxcPolicies.All;

    public IReadOnlyList<string> DisabledPolicies { get; init; } = Array.Empty<string>();

    public bool UsesPlatformDefault { get; init; }

    public bool IsEnabled(string policy) =>
        Enabled && EnabledPolicies.Contains(policy, StringComparer.Ordinal);
}

public static class MxcPolicyResolver
{
    public static MxcEffectivePolicy Resolve(MxcSettings? settings)
    {
        settings ??= new MxcSettings();
        var disabled = MxcPolicies.Normalize(settings.DisabledPolicies);
        var platformDefaultOff = settings.UsesPlatformDefault && !settings.Enabled && disabled.Count == 0;
        if ((!settings.Enabled || disabled.Count > 0) && !platformDefaultOff && !settings.RiskAccepted)
        {
            return new MxcEffectivePolicy();
        }

        return new MxcEffectivePolicy
        {
            Enabled = settings.Enabled,
            EnabledPolicies = MxcPolicies.All.Where(policy => !disabled.Contains(policy, StringComparer.Ordinal)).ToList(),
            DisabledPolicies = disabled,
            UsesPlatformDefault = settings.UsesPlatformDefault,
        };
    }
}
