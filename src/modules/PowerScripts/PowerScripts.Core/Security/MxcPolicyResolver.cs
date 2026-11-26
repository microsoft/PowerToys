// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Security;

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
        };
    }
}
