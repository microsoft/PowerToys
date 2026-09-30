// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Security;

/// <summary>The fully resolved MXC enforcement decision for one script invocation.</summary>
public sealed class MxcEffectivePolicy
{
    public bool Enabled { get; init; } = true;

    public IReadOnlyList<string> EnabledPolicies { get; init; } = MxcPolicies.All;

    public IReadOnlyList<string> DisabledPolicies { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RecommendedPolicies { get; init; } = MxcPolicies.All;

    public bool UsesPlatformDefault { get; init; }

    public bool IsEnabled(string policy) =>
        Enabled && EnabledPolicies.Contains(policy, StringComparer.Ordinal);
}

/// <summary>Combines safe defaults, display-only script recommendations, and user overrides.</summary>
public static class MxcPolicyResolver
{
    /// <summary>
    /// Resolves one script's policy. Recommendations are copied to the result for display only and
    /// never remove enforcement. A global weakening is honored only by global <c>riskAccepted</c>;
    /// a per-script weakening is honored only by that script override's <c>riskAccepted</c>.
    /// Any unaccepted weakening makes the entire result fully restricted rather than partially
    /// applying it, so malformed or hand-edited settings can never silently reduce containment.
    /// </summary>
    public static MxcEffectivePolicy Resolve(
        MxcSettings? settings,
        string scriptId,
        IEnumerable<string>? recommendedPolicies = null)
    {
        settings ??= new MxcSettings();
        var recommendations = MxcPolicies.Normalize(recommendedPolicies);
        if (recommendations.Count == 0 && recommendedPolicies is null)
        {
            recommendations = MxcPolicies.All.ToList();
        }

        var platformDefaultOff = settings.UsesPlatformDefault &&
                                 !settings.Enabled &&
                                 MxcPolicies.Normalize(settings.DisabledPolicies).Count == 0;
        if (RequestsWeakening(settings.Enabled, settings.DisabledPolicies) &&
            !platformDefaultOff &&
            !settings.RiskAccepted)
        {
            return FullyRestricted(recommendations);
        }

        var enabled = settings.Enabled;
        var usesPlatformDefault = settings.UsesPlatformDefault;
        var disabled = settings.RiskAccepted
            ? MxcPolicies.Normalize(settings.DisabledPolicies)
            : new List<string>();

        if (settings.Scripts is not null && settings.Scripts.TryGetValue(scriptId, out var scriptOverride))
        {
            if (scriptOverride.Enabled.HasValue)
            {
                usesPlatformDefault = false;
            }

            var requestedEnabled = scriptOverride.Enabled ?? enabled;
            if (RequestsOverrideWeakening(scriptOverride) &&
                !scriptOverride.RiskAccepted)
            {
                return FullyRestricted(recommendations);
            }

            enabled = requestedEnabled;
            if (scriptOverride.RiskAccepted)
            {
                disabled.AddRange(MxcPolicies.Normalize(scriptOverride.DisabledPolicies));
            }

            foreach (var enabledPolicy in MxcPolicies.Normalize(scriptOverride.EnabledPolicies))
            {
                disabled.RemoveAll(policy => string.Equals(policy, enabledPolicy, StringComparison.Ordinal));
            }
        }

        disabled = disabled.Distinct(StringComparer.Ordinal).ToList();
        var enabledPolicies = MxcPolicies.All.Where(p => !disabled.Contains(p, StringComparer.Ordinal)).ToList();
        return new MxcEffectivePolicy
        {
            Enabled = enabled,
            EnabledPolicies = enabledPolicies,
            DisabledPolicies = disabled,
            RecommendedPolicies = recommendations,
            UsesPlatformDefault = usesPlatformDefault,
        };
    }

    private static bool RequestsWeakening(bool enabled, IEnumerable<string>? disabledPolicies) =>
        !enabled || MxcPolicies.Normalize(disabledPolicies).Count > 0;

    private static bool RequestsOverrideWeakening(MxcScriptOverride scriptOverride) =>
        scriptOverride.Enabled == false || MxcPolicies.Normalize(scriptOverride.DisabledPolicies).Count > 0;

    private static MxcEffectivePolicy FullyRestricted(IReadOnlyList<string> recommendations) =>
        new()
        {
            Enabled = true,
            EnabledPolicies = MxcPolicies.All,
            DisabledPolicies = Array.Empty<string>(),
            RecommendedPolicies = recommendations,
        };
}
