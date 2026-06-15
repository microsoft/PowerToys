// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace KeyboardManagerEditorUI.Templates
{
    public static class TemplateResolver
    {
        public readonly record struct Resolved(string Executable, string Args);

        // Matches {paramName} placeholders. Resolution is single-pass over the original
        // template so a substituted value can never be re-interpreted as another placeholder
        // (prevents substitution-injection once free-text parameters are introduced).
        private static readonly Regex PlaceholderRegex = new(@"\{(\w+)\}");

        // Characters that force argument quoting per CommandLineToArgvW parsing rules.
        private static readonly char[] QuoteTriggers = { ' ', '\t', '\n', '\v', '"' };

        public static Resolved Resolve(
            CommandTemplate template,
            IReadOnlyDictionary<string, string>? values)
        {
            var argsTemplate = template.ArgsTemplate ?? string.Empty;

            // Pre-compute the (quoted-if-needed) replacement for every declared parameter.
            var substitutions = new Dictionary<string, string>();
            foreach (var p in template.Parameters)
            {
                string raw = string.Empty;
                if (values is not null && values.TryGetValue(p.Name, out var v))
                {
                    raw = v ?? string.Empty;
                }

                substitutions[p.Name] = QuoteArgumentIfNeeded(raw);
            }

            // Single pass: each {name} is replaced exactly once against the original template.
            // Unknown placeholders are left untouched (matches prior behavior).
            string args = PlaceholderRegex.Replace(argsTemplate, m =>
                substitutions.TryGetValue(m.Groups[1].Value, out var replacement)
                    ? replacement
                    : m.Value);

            return new Resolved(template.Executable ?? string.Empty, args);
        }

        // Quotes a value for a Windows command line (CommandLineToArgvW rules) only when it
        // contains whitespace or a quote, so simple values (e.g. fixed combo choices) and
        // empty values pass through unchanged.
        internal static string QuoteArgumentIfNeeded(string value)
        {
            if (value.Length == 0 || value.IndexOfAny(QuoteTriggers) < 0)
            {
                return value;
            }

            var sb = new StringBuilder();
            sb.Append('"');

            int backslashes = 0;
            foreach (char c in value)
            {
                if (c == '\\')
                {
                    backslashes++;
                }
                else if (c == '"')
                {
                    // Escape the run of backslashes preceding a quote, then the quote itself.
                    sb.Append('\\', (backslashes * 2) + 1);
                    backslashes = 0;
                    sb.Append('"');
                }
                else
                {
                    sb.Append('\\', backslashes);
                    backslashes = 0;
                    sb.Append(c);
                }
            }

            // Escape trailing backslashes so they don't escape the closing quote.
            sb.Append('\\', backslashes * 2);
            sb.Append('"');
            return sb.ToString();
        }
    }
}
