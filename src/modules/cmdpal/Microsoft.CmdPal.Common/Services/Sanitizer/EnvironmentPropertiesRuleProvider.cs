// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using Microsoft.CmdPal.Common.Services.Sanitizer.Abstraction;

namespace Microsoft.CmdPal.Common.Services.Sanitizer;

internal sealed class EnvironmentPropertiesRuleProvider : ISanitizationRuleProvider
{
    public IEnumerable<SanitizationRule> GetRules()
    {
        List<SanitizationRule> rules = [];

        var machine = Environment.MachineName;
        if (!string.IsNullOrWhiteSpace(machine))
        {
            var rx = new Regex(@"\b" + Regex.Escape(machine) + @"\b", SanitizerDefaults.DefaultOptions, TimeSpan.FromMilliseconds(SanitizerDefaults.DefaultMatchTimeoutMs));
            rules.Add(new(rx, "[MACHINE_NAME_REDACTED]", "Machine name"));
        }

        var domain = Environment.UserDomainName;
        if (!string.IsNullOrWhiteSpace(domain))
        {
            var rx = new Regex(@"\b" + Regex.Escape(domain) + @"\b", SanitizerDefaults.DefaultOptions, TimeSpan.FromMilliseconds(SanitizerDefaults.DefaultMatchTimeoutMs));
            rules.Add(new(rx, "[USER_DOMAIN_NAME_REDACTED]", "User domain name"));
        }

        return rules;
    }
}
