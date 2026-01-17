// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using Microsoft.CmdPal.Core.Common.Services.Sanitizer.Abstraction;

namespace Microsoft.CmdPal.Core.Common.Services.Sanitizer;

internal sealed partial class NetworkRuleProvider : ISanitizationRuleProvider
{
    public IEnumerable<SanitizationRule> GetRules()
    {
        yield return new(Ipv4Rx(), "[IP4_REDACTED]", "IP addresses");
        yield return new(Ipv6BracketedRx(), "[IP6_REDACTED]", "IPv6 addresses (bracketed/with port)");
        yield return new(Ipv6Rx(), "[IP6_REDACTED]", "IPv6 addresses");
        yield return new(MacAddressRx(), "[MAC_ADDRESS_REDACTED]", "MAC addresses");
    }

    [GeneratedRegex(@"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b",
        SanitizerDefaults.DefaultOptions, SanitizerDefaults.DefaultMatchTimeoutMs)]
    private static partial Regex Ipv4Rx();

    [GeneratedRegex(
        """
        (?ix)                                  # ignore case/whitespace
              (?<![A-F0-9:])                        # left edge
              (
                (?:[A-F0-9]{1,4}:){7}[A-F0-9]{1,4}                | # 1:2:3:4:5:6:7:8
                (?:[A-F0-9]{1,4}:){1,7}:                           | # 1:: 1:2:...:7::
                (?:[A-F0-9]{1,4}:){1,6}:[A-F0-9]{1,4}             |
                (?:[A-F0-9]{1,4}:){1,5}(?::[A-F0-9]{1,4}){1,2}    |
                (?:[A-F0-9]{1,4}:){1,4}(?::[A-F0-9]{1,4}){1,3}    |
                (?:[A-F0-9]{1,4}:){1,3}(?::[A-F0-9]{1,4}){1,4}    |
                (?:[A-F0-9]{1,4}:){1,2}(?::[A-F0-9]{1,4}){1,5}    |
                [A-F0-9]{1,4}:(?::[A-F0-9]{1,4}){1,6}             |
                :(?::[A-F0-9]{1,4}){1,7}                          | # ::, ::1, etc.
                (?:[A-F0-9]{1,4}:){6}\d{1,3}(?:\.\d{1,3}){3}      | # IPv4 tail
                (?:[A-F0-9]{1,4}:){1,5}:(?:\d{1,3}\.){3}\d{1,3}   |
                (?:[A-F0-9]{1,4}:){1,4}:(?:\d{1,3}\.){3}\d{1,3}   |
                (?:[A-F0-9]{1,4}:){1,3}:(?:\d{1,3}\.){3}\d{1,3}   |
                (?:[A-F0-9]{1,4}:){1,2}:(?:\d{1,3}\.){3}\d{1,3}   |
                [A-F0-9]{1,4}:(?:\d{1,3}\.){3}\d{1,3}             |
                :(?:\d{1,3}\.){3}\d{1,3}
              )
              (?:%\w+)?                               # optional zone id
              (?![A-F0-9:])                           # right edge
        """,
        SanitizerDefaults.DefaultOptions | RegexOptions.IgnorePatternWhitespace, SanitizerDefaults.DefaultMatchTimeoutMs)]
    private static partial Regex Ipv6Rx();

    [GeneratedRegex(
        """
    (?ix)
          \[
            (
              (?:[A-F0-9]{1,4}:){7}[A-F0-9]{1,4}              |
              (?:[A-F0-9]{1,4}:){1,7}:                         |
              (?:[A-F0-9]{1,4}:){1,6}:[A-F0-9]{1,4}           |
              (?:[A-F0-9]{1,4}:){1,5}(?::[A-F0-9]{1,4}){1,2}  |
              (?:[A-F0-9]{1,4}:){1,4}(?::[A-F0-9]{1,4}){1,3}  |
              (?:[A-F0-9]{1,4}:){1,3}(?::[A-F0-9]{1,4}){1,4}  |
              (?:[A-F0-9]{1,4}:){1,2}(?::[A-F0-9]{1,4}){1,5}  |
              [A-F0-9]{1,4}:(?::[A-F0-9]{1,4}){1,6}           |
              :(?::[A-F0-9]{1,4}){1,7}                        |
              (?:[A-F0-9]{1,4}:){6}\d{1,3}(?:\.\d{1,3}){3}    |
              (?:[A-F0-9]{1,4}:){1,5}:(?:\d{1,3}\.){3}\d{1,3} |
              (?:[A-F0-9]{1,4}:){1,4}:(?:\d{1,3}\.){3}\d{1,3} |
              (?:[A-F0-9]{1,4}:){1,3}:(?:\d{1,3}\.){3}\d{1,3} |
              (?:[A-F0-9]{1,4}:){1,2}:(?:\d{1,3}\.){3}\d{1,3} |
              [A-F0-9]{1,4}:(?:\d{1,3}\.){3}\d{1,3}           |
              :(?:\d{1,3}\.){3}\d{1,3}
            )
            (?:%\w+)?                          # optional zone id
          \]
          (?: : (?<port>\d{1,5}) )?            # optional port
    """,
        SanitizerDefaults.DefaultOptions | RegexOptions.IgnorePatternWhitespace, SanitizerDefaults.DefaultMatchTimeoutMs)]
    private static partial Regex Ipv6BracketedRx();

    [GeneratedRegex(@"\b(?:[0-9A-Fa-f]{2}[:-]){5}(?:[0-9A-Fa-f]{2}|[0-9A-Fa-f]{1,2})\b",
        SanitizerDefaults.DefaultOptions, SanitizerDefaults.DefaultMatchTimeoutMs)]
    private static partial Regex MacAddressRx();
}
