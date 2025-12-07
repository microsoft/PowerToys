// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using Microsoft.CmdPal.Core.Common.Services.Sanitizer.Abstraction;

namespace Microsoft.CmdPal.Common.UnitTests.TestUtils;

/// <summary>
/// Test-only helpers for applying SanitizationRule sets without relying on production ITextSanitizer implementation.
/// </summary>
public static class SanitizerTestHelper
{
    /// <summary>
    /// Applies the provided rules to the input, in order, mimicking the production sanitizer behavior closely
    /// but without any external dependencies.
    /// </summary>
    public static string ApplyRules(string? input, IEnumerable<SanitizationRule> rules)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input ?? string.Empty;
        }

        var result = input;
        foreach (var rule in rules ?? [])
        {
            try
            {
                var previous = result;
                result = rule.Evaluator is null
                    ? rule.Regex.Replace(previous, rule.Replacement ?? string.Empty)
                    : rule.Regex.Replace(previous, rule.Evaluator);

                // Guardrail to avoid accidental mass-redaction from a faulty rule
                if (result.Length < previous.Length * 0.3)
                {
                    result = previous;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Ignore timeouts in tests
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a lightweight sanitizer instance backed by the given rules.
    /// Useful when a component expects an ITextSanitizer, but you want deterministic behavior in tests.
    /// </summary>
    public static ITextSanitizer CreateSanitizer(IEnumerable<SanitizationRule> rules)
        => new InlineSanitizer(rules);

    private sealed class InlineSanitizer : ITextSanitizer
    {
        private readonly List<SanitizationRule> _rules;

        public InlineSanitizer(IEnumerable<SanitizationRule> rules)
        {
            _rules = rules?.ToList() ?? [];
        }

        public string Sanitize(string? input) => ApplyRules(input, _rules);

        public void AddRule(string pattern, string replacement, string description = "")
        {
            var rx = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
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
                // Ignore exceptions for test determinism
            }

            return input;
        }
    }
}
