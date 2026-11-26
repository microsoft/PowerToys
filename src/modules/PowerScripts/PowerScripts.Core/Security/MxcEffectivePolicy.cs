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

    public bool IsEnabled(string policy) =>
        Enabled && EnabledPolicies.Contains(policy, StringComparer.Ordinal);
}
