// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.PowerToys.TextExpander;

internal static partial class SnippetTemplate
{
    private const string ValuePatternText = @"\{\{(?:(?<fixed>date|time|datetime|isodate)|date:(?<format>[^}]*)|prompt(?<optional>\?)?:(?<label>[^}]*))\}\}";

    [GeneratedRegex(ValuePatternText)]
    private static partial Regex ValuePattern();

    [GeneratedRegex(@"\$\|\$|\{\{clipboard\}\}|" + ValuePatternText)]
    private static partial Regex VariablePattern();

    internal static IReadOnlyList<(int Start, int End)> FindVariables(string text)
    {
        var variables = new List<(int Start, int End)>();
        bool cursorPlaced = false;
        foreach (Match match in VariablePattern().Matches(text))
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
        foreach (Match match in ValuePattern().Matches(text))
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
                tokens.Add(match.Groups["fixed"].Value switch
                {
                    "date" => new(SnippetTokenKind.Date, "MM/dd/yy"),
                    "time" => new(SnippetTokenKind.Time, "hh:mm tt"),
                    "datetime" => new(SnippetTokenKind.Date, "MM/dd/yy hh:mm tt"),
                    _ => new(SnippetTokenKind.Date, "yyyy-MM-dd"),
                });
            }

            last = match.Index + match.Length;
        }

        AddLiteral(text.AsSpan(last), tokens, ref cursorPlaced);
        return tokens;
    }

    private static void AddLiteral(ReadOnlySpan<char> text, List<SnippetToken> tokens, ref bool cursorPlaced)
    {
        var literal = new StringBuilder();
        for (int index = 0; index < text.Length;)
        {
            SnippetTokenKind? kind = null;
            int length = 1;
            char character = text[index];
            if (character == '\\' && index + 1 < text.Length && text[index + 1] is 'n' or 't')
            {
                kind = text[index + 1] == 'n' ? SnippetTokenKind.LineBreak : SnippetTokenKind.Tab;
                length = 2;
            }
            else if (character is '\n' or '\t')
            {
                kind = character == '\n' ? SnippetTokenKind.LineBreak : SnippetTokenKind.Tab;
            }
            else if (text[index..].StartsWith("{{clipboard}}", StringComparison.Ordinal))
            {
                kind = SnippetTokenKind.Clipboard;
                length = "{{clipboard}}".Length;
            }
            else if (!cursorPlaced && text[index..].StartsWith(Variables.CursorMarker, StringComparison.Ordinal))
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

            index += length;
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
