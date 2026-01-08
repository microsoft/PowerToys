// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Microsoft.CmdPal.Core.Common.Services.Sanitizer.Abstraction;

namespace Microsoft.CmdPal.Core.Common.Services.Sanitizer;

internal sealed class FilenameMaskRuleProvider : ISanitizationRuleProvider
{
    private static readonly FrozenSet<string> CommonFileStemExclusions = new[]
    {
        "settings",
        "config",
        "configuration",
        "appsettings",
        "options",
        "prefs",
        "preferences",
        "squirrel",
        "app",
        "system",
        "env",
        "environment",
        "manifest",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<SanitizationRule> GetRules()
    {
        const string pattern = """
        (?<full>
            (?: [A-Za-z]: )? (?: [\\/][^\\/:*?""<>|\s]+ )+       # drive-rooted or UNC-like
          |     [^\\/:*?""<>|\s]+ (?: [\\/][^\\/:*?""<>|\s]+ )+  # relative with at least one sep
        )
        """;

        var rx = new Regex(pattern, SanitizerDefaults.DefaultOptions | RegexOptions.IgnorePatternWhitespace, TimeSpan.FromMilliseconds(SanitizerDefaults.DefaultMatchTimeoutMs));
        yield return new SanitizationRule(rx, MatchEvaluator, "Mask filename in any path");
        yield break;

        static string MatchEvaluator(Match m)
        {
            var full = m.Groups["full"].Value;

            var lastSep = Math.Max(full.LastIndexOf('\\'), full.LastIndexOf('/'));
            if (lastSep < 0 || lastSep == full.Length - 1)
            {
                return full;
            }

            var dir = full[..(lastSep + 1)];
            var file = full[(lastSep + 1)..];

            var dot = file.LastIndexOf('.');
            var looksLikeFile = (dot > 0 && dot < file.Length - 1) || (file.StartsWith('.') && file.Length > 1);

            if (!looksLikeFile)
            {
                return full;
            }

            string stem, ext;
            if (dot > 0 && dot < file.Length - 1)
            {
                stem = file[..dot];
                ext = file[dot..];
            }
            else
            {
                stem = file;
                ext = string.Empty;
            }

            if (!ShouldMaskFileName(stem))
            {
                return dir + file;
            }

            var masked = MaskStem(stem) + ext;
            return dir + masked;
        }
    }

    private static string NormalizeStem(string stem)
    {
        return stem.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal);
    }

    private static bool ShouldMaskFileName(string stem)
    {
        return !CommonFileStemExclusions.Contains(NormalizeStem(stem));
    }

    private static string MaskStem(string stem)
    {
        if (string.IsNullOrEmpty(stem))
        {
            return stem;
        }

        var keep = Math.Min(2, stem.Length);
        var maskedCount = Math.Max(1, stem.Length - keep);
        return stem[..keep] + new string('*', maskedCount);
    }
}
