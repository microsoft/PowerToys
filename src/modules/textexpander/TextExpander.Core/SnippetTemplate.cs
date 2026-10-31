// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.PowerToys.TextExpander;

internal static class SnippetTemplate
{
    private const string ValuePatternText = @"\{\{(?:(?<fixed>date|time|datetime|isodate)|date:(?<format>[^}]*)|prompt(?<optional>\?)?:(?<label>[^}]*))\}\}";

    // Without backtracking, a text full of unclosed "{{prompt:" takes linear time, not quadratic. The regex source
    // generator can't build that engine, so these are constructed.
    private static readonly Regex ValuePattern = new(ValuePatternText, RegexOptions.NonBacktracking);

    private static readonly Regex VariablePattern = new(@"\$\|\$|\{\{clipboard\}\}|" + ValuePatternText, RegexOptions.NonBacktracking);

    internal static IReadOnlyList<(int Start, int End)> FindVariables(string text)
    {
        var variables = new List<(int Start, int End)>();
        bool cursorPlaced = false;
        foreach (Match match in VariablePattern.Matches(text))
        {
            if (match.Value == Variables.CursorMarker)
            {
                if (cursorPlaced)
                {
                    continue;
                }

                cursorPlaced = true;
            }

            variables.Add((match.Index, match.Index + match.Length));
        }

        return variables;
    }

    internal static IReadOnlyList<SnippetToken> Parse(string text)
    {
        var tokens = new List<SnippetToken>();
        bool cursorPlaced = false;
        int last = 0;
        foreach (Match match in ValuePattern.Matches(text))
        {
            AddLiteral(text.AsSpan(last, match.Index - last), tokens, ref cursorPlaced);
            if (match.Groups["label"].Success)
            {
                tokens.Add(new(SnippetTokenKind.Prompt, match.Groups["label"].Value, !match.Groups["optional"].Success));
            }
            else if (match.Groups["format"].Success)
            {
                tokens.Add(new(SnippetTokenKind.Date, match.Groups["format"].Value));
            }
            else
            {
                // {{date}}, {{time}}, {{datetime}} and {{isodate}} keep their name; Variables.FormatToken formats each.
                string name = match.Groups["fixed"].Value;
                tokens.Add(new(name == "time" ? SnippetTokenKind.Time : SnippetTokenKind.Date, name, Fixed: true));
            }

            last = match.Index + match.Length;
        }

        AddLiteral(text.AsSpan(last), tokens, ref cursorPlaced);
        return tokens;
    }

    private static void AddLiteral(ReadOnlySpan<char> text, List<SnippetToken> tokens, ref bool cursorPlaced)
    {
        var literal = new StringBuilder();
        while (!text.IsEmpty)
        {
            // Plain text goes in whole, up to the next character that can start a token.
            int next = text.IndexOfAny("\n\t{$");
            if (next < 0)
            {
                literal.Append(text);
                break;
            }

            literal.Append(text[..next]);
            text = text[next..];
            SnippetTokenKind? kind = null;
            int length = 1;
            char character = text[0];
            if (character is '\n' or '\t')
            {
                kind = character == '\n' ? SnippetTokenKind.LineBreak : SnippetTokenKind.Tab;
            }
            else if (text.StartsWith("{{clipboard}}", StringComparison.Ordinal))
            {
                kind = SnippetTokenKind.Clipboard;
                length = "{{clipboard}}".Length;
            }
            else if (!cursorPlaced && text.StartsWith(Variables.CursorMarker, StringComparison.Ordinal))
            {
                kind = SnippetTokenKind.Cursor;
                length = Variables.CursorMarker.Length;
                cursorPlaced = true;
            }

            if (kind is { } tokenKind)
            {
                FlushLiteral(literal, tokens);
                tokens.Add(new(tokenKind));
            }
            else
            {
                literal.Append(character);
            }

            text = text[length..];
        }

        FlushLiteral(literal, tokens);
    }

    private static void FlushLiteral(StringBuilder literal, List<SnippetToken> tokens)
    {
        if (literal.Length > 0)
        {
            tokens.Add(new(SnippetTokenKind.Text, literal.ToString()));
            literal.Clear();
        }
    }
}
