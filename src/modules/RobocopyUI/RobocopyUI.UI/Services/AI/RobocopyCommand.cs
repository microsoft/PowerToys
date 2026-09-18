// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// Parses and renders robocopy command lines. Centralizing this keeps the AI-generated preview,
    /// the hand-built UI preview, and the text actually handed to robocopy.exe in agreement about
    /// quoting and switch formatting.
    /// </summary>
    public static class RobocopyCommand
    {
        /// <summary>
        /// The executable name used in rendered command lines.
        /// </summary>
        public const string Executable = "robocopy.exe";

        /// <summary>
        /// Renders a full command line, including the executable name.
        /// </summary>
        public static string Render(string source, string destination, IReadOnlyList<RobocopyPlanOption> options)
            => $"{Executable} {RenderArguments(source, destination, options)}";

        /// <summary>
        /// Renders just the arguments, without the executable name, for passing to <c>robocopy.exe</c>.
        /// </summary>
        public static string RenderArguments(string source, string destination, IReadOnlyList<RobocopyPlanOption> options)
        {
            var builder = new StringBuilder();
            builder.Append(Quote(source)).Append(' ').Append(Quote(destination));

            if (options is not null)
            {
                foreach (var option in options)
                {
                    var rendered = RenderOption(option);
                    if (rendered.Length > 0)
                    {
                        builder.Append(' ').Append(rendered);
                    }
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Switches whose values are a space-separated list of file or directory specs. Robocopy expects
        /// these as separate arguments after the switch (<c>/XF *.tmp *.log</c>), not in the colon form
        /// (<c>/XF:*.tmp *.log</c>) used by every other valued switch.
        /// </summary>
        private static readonly HashSet<string> SpaceSeparatedSwitches =
            new(StringComparer.OrdinalIgnoreCase) { "/XF", "/XD" };

        /// <summary>
        /// Renders a single switch, e.g. <c>/E</c>, <c>/R:5</c> or <c>/XF *.tmp</c>.
        /// </summary>
        public static string RenderOption(RobocopyPlanOption option)
        {
            if (option is null || string.IsNullOrWhiteSpace(option.Name))
            {
                return string.Empty;
            }

            var name = option.Name.Trim();

            if (string.IsNullOrEmpty(option.Value))
            {
                return name;
            }

            return SpaceSeparatedSwitches.Contains(name)
                ? $"{name} {option.Value.Trim()}"
                : $"{name}:{option.Value}";
        }

        /// <summary>
        /// Splits a switch token such as <c>/R:5</c> into its name and value. Only the first colon
        /// separates the two, because values such as run hours (<c>/RH:0100-2300</c>) and copy flags
        /// may themselves contain punctuation.
        /// </summary>
        public static RobocopyPlanOption ParseOption(string token)
        {
            ArgumentNullException.ThrowIfNull(token);

            var trimmed = token.Trim();
            var separator = trimmed.IndexOf(':', StringComparison.Ordinal);

            return separator < 0
                ? new RobocopyPlanOption(trimmed, string.Empty)
                : new RobocopyPlanOption(trimmed[..separator], trimmed[(separator + 1)..]);
        }

        /// <summary>
        /// Quotes a path when it contains whitespace, after stripping any quotes it already carries.
        /// </summary>
        public static string Quote(string path)
        {
            var trimmed = (path ?? string.Empty).Trim().Trim('"');
            return trimmed.Contains(' ', StringComparison.Ordinal) ? $"\"{trimmed}\"" : trimmed;
        }

        /// <summary>
        /// Drops switches that are contradictory or already implied by another switch.
        /// </summary>
        /// <remarks>
        /// The more specific switch wins: /S over /E, and /MIR over the /E and /PURGE it expands to.
        /// /COPYALL implies /COPY and /SEC. /X only reports extra files, so it is dropped next to a
        /// switch that already acts on them.
        /// </remarks>
        public static void Prune(List<RobocopyPlanOption> options)
        {
            ArgumentNullException.ThrowIfNull(options);

            bool Has(string name) => options.Any(option => string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase));
            void Drop(string name) => options.RemoveAll(option => string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase));

            if (Has("/MIR"))
            {
                Drop("/E");
                Drop("/S");
                Drop("/PURGE");
            }
            else if (Has("/S"))
            {
                Drop("/E");
            }

            if (Has("/COPYALL"))
            {
                Drop("/COPY");
                Drop("/SEC");
            }

            if (Has("/XX") || Has("/MIR") || Has("/PURGE"))
            {
                Drop("/X");
            }
        }

        /// <summary>
        /// Returns deterministic warnings for switches that delete or move data.
        /// </summary>
        public static IReadOnlyList<string> GetDestructiveWarnings(IEnumerable<RobocopyPlanOption> options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var warnings = new List<string>();

            foreach (var option in options)
            {
                var text = option.Name.ToUpperInvariant() switch
                {
                    "/MIR" => "/MIR mirrors the source: files that exist only in the destination will be deleted.",
                    "/PURGE" => "/PURGE deletes destination files and folders that no longer exist in the source.",
                    "/MOVE" => "/MOVE deletes the files and folders from the source after they are copied.",
                    "/MOV" => "/MOV deletes the files from the source after they are copied.",
                    _ => null,
                };

                if (text is not null && !warnings.Contains(text, StringComparer.Ordinal))
                {
                    warnings.Add(text);
                }
            }

            return warnings;
        }

        /// <summary>
        /// Parses a rendered command line back into its parts. Accepts input with or without a leading
        /// <c>robocopy</c>/<c>robocopy.exe</c>, and honors double-quoted paths.
        /// </summary>
        /// <returns><see langword="true"/> when both a source and a destination were found.</returns>
        public static bool TryParse(string commandLine, out string source, out string destination, out IReadOnlyList<RobocopyPlanOption> options)
        {
            source = string.Empty;
            destination = string.Empty;
            options = [];

            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return false;
            }

            var tokens = Tokenize(commandLine);
            var parsedOptions = new List<RobocopyPlanOption>();
            var positionals = new List<string>();

            foreach (var token in tokens)
            {
                if (token.StartsWith('/'))
                {
                    parsedOptions.Add(ParseOption(token));
                }
                else if (positionals.Count == 0
                    && (token.Equals(Executable, StringComparison.OrdinalIgnoreCase)
                        || token.Equals("robocopy", StringComparison.OrdinalIgnoreCase)))
                {
                    // Leading executable name, not a path.
                    continue;
                }
                else
                {
                    positionals.Add(token);
                }
            }

            options = parsedOptions;

            if (positionals.Count > 0)
            {
                source = positionals[0];
            }

            if (positionals.Count > 1)
            {
                destination = positionals[1];
            }

            return positionals.Count >= 2;
        }

        /// <summary>
        /// Splits a command line on whitespace, treating double-quoted spans as single tokens.
        /// </summary>
        private static List<string> Tokenize(string commandLine)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            foreach (var c in commandLine)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }

                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
            }

            return tokens;
        }
    }
}
