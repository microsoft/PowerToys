// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Microsoft.CmdPal.Common.Services.Sanitizer.Abstraction;

namespace Microsoft.CmdPal.Common.Services.Sanitizer;

internal sealed class SecretKeyValueRulesProvider : ISanitizationRuleProvider
{
    // Central list of common secret keys/phrases to redact when found in key=value pairs.
    private static readonly FrozenSet<string> SecretKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Core passwords/secrets
        "password",
        "passphrase",
        "passwd",
        "pwd",

        // Tokens
        "token",
        "access token",
        "refresh token",
        "id token",
        "auth token",
        "session token",
        "bearer token",
        "personal access token",
        "pat",

        // API / client credentials
        "api key",
        "api secret",
        "x api key",
        "client id",
        "client secret",
        "x client id",
        "x client secret",
        "consumer secret",
        "service principal secret",

        // Cloud & platform (Azure/AppInsights/etc.)
        "subscription key",
        "instrumentation key",
        "account key",
        "storage account key",
        "shared access key",
        "shared access signature",
        "SAS token",

        // Connection strings (often surfaced in exception messages)
        "connection string",
        "conn string",
        "storage connection string",

        // Certificates & crypto
        "private key",
        "certificate password",
        "client certificate password",
        "pfx password",

        // AWS common keys
        "aws access key id",
        "aws secret access key",
        "aws session token",

        // Optional service aliases
        "cosmos db key",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<SanitizationRule> GetRules()
    {
        yield return BuildSecretKeyValueRule(
            SecretKeys,
            timeout: TimeSpan.FromSeconds(5),
            starEverything: true);
    }

    private static SanitizationRule BuildSecretKeyValueRule(
        IEnumerable<string> keys,
        RegexOptions? options = null,
        TimeSpan? timeout = null,
        string label = "[REDACTED]",
        bool treatDashUnderscoreAsSpace = true,
        string separatorsClass = "[:=]",     // char class for separators
        string unquotedStopClass = "\\s",
        bool starEverything = false)
    {
        ArgumentNullException.ThrowIfNull(keys);

        // Between-word matcher for keys: "api key" -> "api\s*key" (optionally treating _/- as "space")
        var between = treatDashUnderscoreAsSpace ? @"(?:\s|[_-])*" : @"\s*";

        var patterns = new List<string>();

        foreach (var raw in keys)
        {
            var key = raw?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (starEverything && key is not ['*', ..])
            {
                key = "*" + key;
            }

            if (key is ['*', .. var tail])
            {
                // Wildcard prefix: allow one non-space token + optional "-" or "_" before the remainder.
                // Matches: "api key", "api-key", "azure-api-key", "user_api_key"
                var remainder = tail.Trim();
                if (remainder.Length == 0)
                {
                    continue;
                }

                var rem = Normalize(remainder, between);
                patterns.Add($@"(?:(?>[A-Za-z0-9_]{{1,128}}[_-]))?{rem}");
            }
            else
            {
                patterns.Add(Normalize(key, between));
            }
        }

        if (patterns.Count == 0)
        {
            throw new ArgumentException("No non-empty keys provided.", nameof(keys));
        }

        var keysAlt = string.Join("|", patterns);

        var pattern =
            $"""
             # Negative lookbehind to ensure the key is not part of a larger word
             (?<![A-Za-z0-9])
             # Match and capture the key (from the provided list)
             (?<key>(?:{keysAlt}))
             # Negative lookahead to ensure the key is not part of a larger word
             (?![A-Za-z0-9])
             # Optional whitespace between key and separator
             \s*
             # Separator (e.g., ':' or '=')
             (?<sep>{separatorsClass})
             # Optional whitespace after separator
             \s*
             # Match and capture the value, supporting quoted or unquoted values
             (?:
                 # Quoted value: match opening quote, value, and closing quote
                 (?<q>["'])(?<val>[^"']+)\k<q>
                 |
                 # Unquoted value: match up to the next whitespace
                 (?<val>[^{unquotedStopClass}]+)
             )
             """;

        var rx = new Regex(
            pattern,
            (options ?? (RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) | RegexOptions.IgnorePatternWhitespace,
            timeout ?? TimeSpan.FromMilliseconds(1000));

        var replacement = @"${key}${sep} ${q}" + label + @"${q}";
        return new SanitizationRule(rx, replacement, "Sensitive key/value pairs");

        static string Normalize(string s, string betweenSep)
            => Regex.Escape(s).Replace("\\ ", betweenSep);
    }
}
