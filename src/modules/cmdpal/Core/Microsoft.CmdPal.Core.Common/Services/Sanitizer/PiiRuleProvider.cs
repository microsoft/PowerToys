// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using Microsoft.CmdPal.Core.Common.Services.Sanitizer.Abstraction;

namespace Microsoft.CmdPal.Core.Common.Services.Sanitizer;

internal sealed partial class PiiRuleProvider : ISanitizationRuleProvider
{
    public IEnumerable<SanitizationRule> GetRules()
    {
        yield return new(EmailRx(), "[EMAIL_REDACTED]", "Email addresses");
        yield return new(SsnRx(), "[SSN_REDACTED]", "Social Security Numbers");
        yield return new(CreditCardRx(), "[CARD_REDACTED]", "Credit card numbers");

        // phone number regex is the most generic, so it goes last
        // we can't make this too generic; otherwise we over-redact error codes, dates, etc.
        yield return new(PhoneRx(), "[PHONE_REDACTED]", "Phone numbers");
    }

    [GeneratedRegex(@"\b[a-zA-Z0-9]([a-zA-Z0-9._%-]*[a-zA-Z0-9])?@[a-zA-Z0-9]([a-zA-Z0-9.-]*[a-zA-Z0-9])?\.[a-zA-Z]{2,}\b",
        SanitizerDefaults.DefaultOptions, SanitizerDefaults.DefaultMatchTimeoutMs)]
    private static partial Regex EmailRx();

    [GeneratedRegex("""
                (?xi)
                # ---------- boundaries ----------
                (?<!\w)                          # not after a letter/digit/underscore
                (?<![A-Za-z0-9]-)                # avoid starting inside hyphenated tokens (GUID middles, etc.)

                # ---------- global do-not-match guards ----------
                (?!                              # ISO date (yyyy-mm-dd / yyyy.mm.dd / yyyy/mm/dd)
                   (?:19|20)\d{2}[-./](?:0[1-9]|1[0-2])[-./](?:0[1-9]|[12]\d|3[01])\b
                )
                (?!                              # EU date (dd-mm-yyyy / dd.mm.yyyy / dd/mm/yyyy)
                   (?:0[1-9]|[12]\d|3[01])[-./](?:0[1-9]|1[0-2])[-./](?:19|20)\d{2}\b
                )
                (?!                              # ISO datetime like 2025-08-24T14:32[:ss][Z|±hh:mm]
                   (?:19|20)\d{2}-\d{2}-\d{2}[T\s]\d{2}:\d{2}(?::\d{2})?(?:Z|[+-]\d{2}:\d{2})?\b
                )
                (?!\b(?:\d{1,3}\.){3}\d{1,3}(?::\d{1,5})?\b)   # IPv4 with optional :port
                (?!\b[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\b)  # GUID, lowercase
                (?!\b[0-9A-F]{8}-(?:[0-9A-F]{4}-){3}[0-9A-F]{12}\b)  # GUID, uppercase
                (?!\bv?\d+(?:\.\d+){2,}\b)       # semantic/file versions like 1.2.3 or 10.0.22631.3448
                (?!\b(?:[0-9A-F]{2}[:-]){5}[0-9A-F]{2}\b)       # MAC address

                # ---------- digit budget ----------
                (?=(?:[^\r\n]*\d){7,15}[^\r\n]*(?:\r\n|$))
                (?=(?:\D*\d){7,15})              # 7–15 digits in total

                # ---------- number body ----------
                (?:
                # A with explicit country code, allow compact digits (E.164-ish) or grouped
                  (?:\+|00)[1-9]\d{0,2}
                  (?:
                    [\p{Zs}.\-\/]*\d{6,14}
                    |
                    [\p{Zs}.\-\/]* (?:\(\d{1,4}\)|\d{1,4})
                    (?:[\p{Zs}.\-\/]+(?:\(\d{2,4}\)|\d{2,4})){1,6}
                  )
                  |
                      # B no country code => require separators between blocks (avoid plain big ints)
                  (?:\(\d{1,4}\)|\d{1,4})
                  (?:[\p{Zs}.\-\/]+(?:\(\d{2,4}\)|\d{2,4})){1,6}
                )

                # ---------- optional extension ----------
                (?:[\p{Zs}.\-,:;]* (?:ext\.?|x) [\p{Zs}]* (?<ext>\d{1,6}))?

                # ---------- end boundary (allow whitespace/newlines at edges) ----------
                (?!-\w)                          # don't end just before '-letter'/'-digit'
                (?!\w)                           # don't be immediately followed by a word char
                """,
    SanitizerDefaults.DefaultOptions | RegexOptions.IgnorePatternWhitespace,
    SanitizerDefaults.DefaultMatchTimeoutMs)]
    private static partial Regex PhoneRx();

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b",
        SanitizerDefaults.DefaultOptions, SanitizerDefaults.DefaultMatchTimeoutMs)]
    private static partial Regex SsnRx();

    [GeneratedRegex(@"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b",
        SanitizerDefaults.DefaultOptions, SanitizerDefaults.DefaultMatchTimeoutMs)]
    private static partial Regex CreditCardRx();
}
