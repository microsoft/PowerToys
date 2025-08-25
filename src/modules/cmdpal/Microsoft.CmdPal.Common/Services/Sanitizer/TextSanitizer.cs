// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.CmdPal.Common.Services.Sanitizer.Abstraction;

namespace Microsoft.CmdPal.Common.Services.Sanitizer;

/// <summary>
/// Generic text sanitizer that applies a sequence of regex-based rules over input text.
/// </summary>
internal sealed class TextSanitizer : ITextSanitizer
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(SanitizerDefaults.DefaultMatchTimeoutMs);

    private readonly List<SanitizationRule> _rules = [];

    public TextSanitizer()
    {
    }

    public TextSanitizer(params IEnumerable<SanitizationRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToList();
    }

    public TextSanitizer(params IEnumerable<ISanitizationRuleProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        foreach (var p in providers)
        {
            try
            {
                _rules.AddRange(p.GetRules());
            }
            catch
            {
                // Best-effort; ignore provider errors
            }
        }
    }

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

    public void AddRule(string pattern, string replacement, string description = "")
    {
        var rx = new Regex(pattern, SanitizerDefaults.DefaultOptions, DefaultTimeout);
        _rules.Add(new SanitizationRule(rx, replacement, description));
    }

    public void RemoveRule(string description)
    {
        _rules.RemoveAll(r => r.Description.Equals(description, StringComparison.OrdinalIgnoreCase));
    }

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
}
