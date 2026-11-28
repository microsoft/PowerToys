// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>What group policy says about Text Expander.</summary>
internal enum PolicyRule
{
    NotConfigured,
    Disabled,
    Enabled,
}

/// <summary>
/// The group policies that turn Text Expander on or off, read as <c>src/common/utils/gpo.h</c> does: Text Expander's
/// own policy, then the policy for all utilities when Text Expander's isn't set.
/// </summary>
internal static class TextExpanderPolicy
{
    internal const string TextExpanderValueName = "ConfigureEnabledUtilityTextExpander";

    internal const string AllUtilitiesValueName = "ConfigureGlobalUtilityEnabledState";

    public static PolicyRule Evaluate(IPolicyStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        var own = ToRule(store.Read(TextExpanderValueName));
        return own != PolicyRule.NotConfigured ? own : ToRule(store.Read(AllUtilitiesValueName));
    }

    // gpo.h treats any other value as a mistake in the policy, the same as not configured.
    private static PolicyRule ToRule(int? value) => value switch
    {
        0 => PolicyRule.Disabled,
        1 => PolicyRule.Enabled,
        _ => PolicyRule.NotConfigured,
    };
}
