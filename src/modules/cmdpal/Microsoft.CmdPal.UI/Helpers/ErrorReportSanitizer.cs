// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Crude and slow blind sanitizer removing PIIs and sensitive information.
/// </summary>
/// <remarks>
/// TODO: If we keep this, it would be a good idea to split it into smaller classes, write unit tests.
/// </remarks>
internal sealed partial class ErrorReportSanitizer
{
    private const RegexOptions DefaultOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const int DefaultMatchTimeoutMs = 100;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(DefaultMatchTimeoutMs);

    private readonly List<SanitizationRule> _rules;
    private readonly Dictionary<string, string> _profilePaths;
    private readonly HashSet<string> _usernames;

    private static readonly FrozenSet<string> CommonPathParts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Users", "home", "Documents", "Desktop", "AppData", "Local", "Roaming",
        "Pictures", "Videos", "Music", "Downloads", "Program Files", "Windows",
        "System32", "bin", "usr", "var", "etc", "opt", "tmp",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> CommonWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "user", "test", "guest", "public", "system", "service",
        "default", "temp", "local", "shared", "common", "data", "config",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

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
        "sas token",

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
        "cosmosdb key",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> CommonFileStemExclusions = new[]
    {
        "settings", "config", "configuration", "appsettings", "options", "prefs", "preferences", "squirrel", "app", "system", "env", "environment", "manifest",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public ErrorReportSanitizer()
    {
        _profilePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _usernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        DetectSystemPaths();
        _rules = BuildSanitizationRules();
    }

    private void DetectSystemPaths()
    {
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile) && Directory.Exists(userProfile))
            {
                _profilePaths.Add(userProfile, "[USER_PROFILE_DIR]");
                var username = Path.GetFileName(userProfile);
                if (!string.IsNullOrEmpty(username) && username.Length > 2)
                {
                    _usernames.Add(username);
                }
            }

            ReadOnlySpan<Environment.SpecialFolder> profileFolders =
            [
                Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolder.Desktop,
                Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolder.MyPictures,
                Environment.SpecialFolder.MyVideos,
                Environment.SpecialFolder.MyMusic,
                Environment.SpecialFolder.DesktopDirectory
            ];

            foreach (var folder in profileFolders)
            {
                var dir = Environment.GetFolderPath(folder);
                if (string.IsNullOrEmpty(dir))
                {
                    continue;
                }

                _profilePaths.Add(dir, $"[{folder.ToString().ToUpperInvariant()}_DIR]");

                var pathParts = dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (var part in pathParts)
                {
                    if (!string.IsNullOrEmpty(part) && part.Length > 2 && !CommonPathParts.Contains(part))
                    {
                        _usernames.Add(part);
                    }
                }
            }

            ReadOnlySpan<string> envVars = ["USERPROFILE", "HOME", "OneDrive", "OneDriveCommercial"];
            foreach (var envVar in envVars)
            {
                var envPath = Environment.GetEnvironmentVariable(envVar);
                if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
                {
                    _profilePaths.Add(envPath, $"[{envVar.ToUpperInvariant()}_DIR]");
                }
            }
        }
        catch
        {
            // Best-effort; fall back to generic rules.
        }
    }

    private List<SanitizationRule> BuildSanitizationRules()
    {
        List<SanitizationRule> rules =
        [
            new(EmailRx(),           "[EMAIL_REDACTED]",                    "Email addresses"),
            new(PhoneRx(),           "[PHONE_REDACTED]",                    "Phone numbers"),
            new(SsnRx(),             "[SSN_REDACTED]",                      "Social Security Numbers"),
            new(CreditCardRx(),      "[CARD_REDACTED]",                     "Credit card numbers"),
            new(UrlRx(),             "[URL_REDACTED]",                      "URLs"),
            new(IpRx(),              "[IP4_REDACTED]",                       "IP addresses"),
            new(Ipv6BracketedRx(),   "[IP6_REDACTED]",                       "IPv6 addresses (bracketed/with port)"),
            new(Ipv6Rx(),            "[IP6_REDACTED]",                       "IPv6 addresses"),
            new(ConnectionParamRx(), "$1=[REDACTED]",                       "Connection string parameters"),
            BuildSecretKeyValueRule(SecretKeys, timeout: TimeSpan.FromSeconds(5), starEverything: true),
            new(JwtRx(),             "[JWT_REDACTED]",                      "JSON Web Tokens (JWTs)"),
            new(TokenRx(),           "[TOKEN_REDACTED]",                    "Potential API keys/tokens"),
            new(MacAddressRx(),      "[MAC_ADDRESS_REDACTED]",              "MAC addresses"),
        ];

        // Order-specific rules
        AddRulesForEnvironmentProperties(rules);
        AddGenericFilenameMaskRule(rules);
        AddProfilePathRules(rules);
        AddUsernameRules(rules);

        return rules;
    }

    private static void AddRulesForEnvironmentProperties(List<SanitizationRule> rules)
    {
        var machine = Environment.MachineName;
        if (!string.IsNullOrWhiteSpace(machine))
        {
            var rx = new Regex(@"\b" + Regex.Escape(machine) + @"\b", DefaultOptions, DefaultTimeout);
            rules.Add(new(rx, "[MACHINE_NAME_REDACTED]", "Machine name"));
        }

        // On some platforms this may be empty; guard it.
        var domain = Environment.UserDomainName;
        if (!string.IsNullOrWhiteSpace(domain))
        {
            var rx = new Regex(@"\b" + Regex.Escape(domain) + @"\b", DefaultOptions, DefaultTimeout);
            rules.Add(new(rx, "[USER_DOMAIN_NAME_REDACTED]", "User domain name"));
        }
    }

    private void AddProfilePathRules(List<SanitizationRule> rules)
    {
        // TODO: not technically correct
        var orderedRules = _profilePaths
            .Where(p => !string.IsNullOrEmpty(p.Key))
            .OrderByDescending(p => p.Key.Length);

        foreach (var profilePath in orderedRules)
        {
            try
            {
                var normalizedPath = profilePath.Key
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                var escapedPath = Regex.Escape(normalizedPath);

                // Match the profile path
                var pattern = escapedPath + @"(?:[/\\]*)";
                var rx = new Regex(pattern, DefaultOptions, DefaultTimeout);

                rules.Add(new(rx, profilePath.Value, $"Profile path: {profilePath}"));
            }
            catch
            {
                // Skip problematic paths
            }
        }
    }

    private static void AddGenericFilenameMaskRule(List<SanitizationRule> rules)
    {
        // Match absolute or relative paths with at least one separator, capture entire path.
        // Windows: [A:]\foo\bar\baz.txt   UNC: \\srv\share\file.ext
        // POSIX:   /usr/local/bin/tool    Relative: src/app/appsettings.json
        const string pattern = """
        (?ix)
        (?<full>
            (?: [A-Za-z]: )? (?: [\\/][^\\/:*?"<>|\s]+ )+      # drive-rooted or UNC-like
          |     [^\\/:*?"<>|\s]+ (?: [\\/][^\\/:*?"<>|\s]+ )+  # relative with at least one sep
        )
        """;

        var rx = new Regex(pattern, DefaultOptions | RegexOptions.IgnorePatternWhitespace, DefaultTimeout);
        rules.Add(new SanitizationRule(rx, MatchEvaluator, "Mask filename in any path"));
        return;

        string MatchEvaluator(Match m)
        {
            var full = m.Groups["full"].Value;

            // Find last separator
            var lastSep = Math.Max(full.LastIndexOf('\\'), full.LastIndexOf('/'));
            if (lastSep < 0 || lastSep == full.Length - 1)
            {
                // No filename (or ends with a separator) -> keep as-is
                return full;
            }

            var dir = full[..(lastSep + 1)]; // keep trailing separator
            var file = full[(lastSep + 1)..];

            // Heuristic: has an extension (a.b) or is a dotfile (.env)
            var dot = file.LastIndexOf('.');
            var looksLikeFile = (dot > 0 && dot < file.Length - 1) || (file.StartsWith('.') && file.Length > 1);

            if (!looksLikeFile)
            {
                return full; // treat as directory name; do nothing
            }

            // Split into stem + extension
            string stem, ext;
            if (dot > 0 && dot < file.Length - 1)
            {
                stem = file[..dot];
                ext = file[dot..]; // includes '.'
            }
            else
            {
                stem = file;
                ext = string.Empty;
            }

            // Skip masking for common config-like file names
            if (!ShouldMaskFileName(stem))
            {
                return dir + file;
            }

            var masked = MaskStem(stem) + ext;
            return dir + masked;
        }
    }

    private static string NormalizeStem(string s)
        => s.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal);

    private static bool ShouldMaskFileName(string stem)
        => !CommonFileStemExclusions.Contains(NormalizeStem(stem));

    private static string MaskStem(string stem)
    {
        if (string.IsNullOrEmpty(stem))
        {
            return stem;
        }

        // don't use 'x' or similar; use '*' to avoid confusion with actual file names
        var keep = Math.Min(2, stem.Length);
        var maskedCount = Math.Max(1, stem.Length - keep);
        return stem[..keep] + new string('*', maskedCount);
    }

    private void AddUsernameRules(List<SanitizationRule> rules)
    {
        foreach (var username in _usernames.Where(u => !string.IsNullOrEmpty(u) && u.Length > 2))
        {
            try
            {
                if (!IsLikelyUsername(username))
                {
                    continue;
                }

                var rx = new Regex(@"\b" + Regex.Escape(username) + @"\b", DefaultOptions, DefaultTimeout);
                rules.Add(new(rx, "[USERNAME_REDACTED]", $"Username: {username}"));
            }
            catch
            {
                // Skip problematic usernames
            }
        }
    }

    private static bool IsLikelyUsername(string username) =>
        !CommonWords.Contains(username) &&
        username.Length is >= 3 and <= 50 &&
        !username.All(char.IsDigit);

    public string Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input ?? string.Empty;
        }

        var result = input;

        foreach (var rule in _rules)
        {
            try
            {
                var previous = result;

                result = rule.Evaluator is null
                    ? rule.Regex.Replace(previous, rule.Replacement!)
                    : rule.Regex.Replace(previous, rule.Evaluator);

                if (result.Length < previous.Length * 0.3)
                {
                    result = previous; // Guardrail
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Ignore timeouts; keep the original input
            }
            catch
            {
                // Ignore other exceptions; keep the original input
            }
        }

        return result;
    }

    public string SanitizeException(Exception? exception)
    {
        if (exception is null)
        {
            return string.Empty;
        }

        var fullMessage = GetFullExceptionMessage(exception);
        return Sanitize(fullMessage);
    }

    private static string GetFullExceptionMessage(Exception exception)
    {
        List<string> messages = [];
        var current = exception;
        var depth = 0;

        // Prevent infinite loops on pathological InnerException graphs
        while (current is not null && depth < 10)
        {
            messages.Add($"{current.GetType().Name}: {current.Message}");

            if (!string.IsNullOrEmpty(current.StackTrace))
            {
                messages.Add($"Stack Trace: {current.StackTrace}");
            }

            current = current.InnerException;
            depth++;
        }

        return string.Join(Environment.NewLine, messages);
    }

    public void AddRule(string pattern, string replacement, string description = "")
    {
        var rx = new Regex(pattern, DefaultOptions, DefaultTimeout);
        _rules.Add(new SanitizationRule(rx, replacement, description));
    }

    public void RemoveRule(string description)
    {
        _rules.RemoveAll(r => r.Description.Equals(description, StringComparison.OrdinalIgnoreCase));
    }

    public IDictionary<string, string> GetDetectedProfilePaths() => _profilePaths.ToDictionary().AsReadOnly();

    public IReadOnlyList<string> GetDetectedUsernames() => _usernames.ToList().AsReadOnly();

    public IReadOnlyList<SanitizationRule> GetRules() => _rules.AsReadOnly();

    public string TestRule(string input, string ruleDescription)
    {
        var rule = _rules.FirstOrDefault(r => r.Description.Contains(ruleDescription, StringComparison.OrdinalIgnoreCase));
        if (rule.Regex is null)
        {
            return input;
        }

        try
        {
            if (rule.Evaluator is not null)
            {
                return rule.Regex.Replace(input, rule.Evaluator);
            }

            if (rule.Replacement is not null)
            {
                return rule.Regex.Replace(input, rule.Replacement);
            }
        }
        catch
        {
            // Ignore exceptions; return original input
        }

        return input;
    }

    [GeneratedRegex(@"\b[a-zA-Z0-9]([a-zA-Z0-9._%-]*[a-zA-Z0-9])?@[a-zA-Z0-9]([a-zA-Z0-9.-]*[a-zA-Z0-9])?\.[a-zA-Z]{2,}\b",
        DefaultOptions, DefaultMatchTimeoutMs)]
    private static partial Regex EmailRx();

    [GeneratedRegex(@"\b(?:\+?1[-.\s]?)?\(?[2-9][0-8][0-9]\)?[-.\s]?[2-9][0-9]{2}[-.\s]?[0-9]{4}\b",
        DefaultOptions, DefaultMatchTimeoutMs)]
    private static partial Regex PhoneRx();

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b",
        DefaultOptions, DefaultMatchTimeoutMs)]
    private static partial Regex SsnRx();

    [GeneratedRegex(@"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b",
        DefaultOptions, DefaultMatchTimeoutMs)]
    private static partial Regex CreditCardRx();

    [GeneratedRegex(@"\b(?:https?|ftp|ftps|file|jdbc|ldap|mailto)://[^\s<>" + "\"'{}\\[\\]\\\\^`|]+",
        DefaultOptions, DefaultMatchTimeoutMs)]
    private static partial Regex UrlRx();

    [GeneratedRegex(@"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b",
        DefaultOptions, DefaultMatchTimeoutMs)]
    private static partial Regex IpRx();

    [GeneratedRegex(@"(Server|Data Source|Initial Catalog|Database|User ID|Username|Password|Pwd|Uid)\s*=\s*[^;,\s]+",
        DefaultOptions, DefaultMatchTimeoutMs)]
    private static partial Regex ConnectionParamRx();

    [GeneratedRegex(@"\b[A-Za-z0-9]{32,128}\b",
        DefaultOptions, DefaultMatchTimeoutMs)]
    private static partial Regex TokenRx();

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
    DefaultOptions | RegexOptions.IgnorePatternWhitespace, DefaultMatchTimeoutMs)]
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
        DefaultOptions | RegexOptions.IgnorePatternWhitespace, DefaultMatchTimeoutMs)]
    private static partial Regex Ipv6BracketedRx();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b",
        DefaultOptions, DefaultMatchTimeoutMs)]
    private static partial Regex JwtRx();

    [GeneratedRegex(@"\b(?:[0-9A-Fa-f]{2}[:-]){5}(?:[0-9A-Fa-f]{2}|[0-9A-Fa-f]{1,2})\b",
        DefaultOptions, DefaultMatchTimeoutMs)]
    private static partial Regex MacAddressRx();

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
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var key = raw.Trim();
            if (starEverything && key[0] != '*')
            {
                key = "*" + key;
            }

            if (key.StartsWith('*'))
            {
                // Wildcard prefix: allow one non-space token + optional "-" or "_" before the remainder.
                // Matches: "api key", "api-key", "azure-api-key", "user_api_key"
                var remainder = key[1..].Trim();
                if (remainder.Length == 0)
                {
                    continue;
                }

                var rem = Normalize(remainder, between);
                patterns.Add($@"(?:\S+[_-])?{rem}");
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

        // Preserve key + sep; redact only the value. Quoted or unquoted values supported.
        var pattern =
            $"""
             (?<![A-Za-z0-9])(?<key>(?:{keysAlt}))(?![A-Za-z0-9])\s*(?<sep>{separatorsClass})\s*
             (?:(?<q>["'])(?<val>[^"']+)\k<q>|(?<val>[^{unquotedStopClass}]+))
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
