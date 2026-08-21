// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.Utils;

internal static class ImplicitWildcardQueryBuilder
{
    private const int MinimumContainsTermLength = 3;

    internal static ImplicitWildcardExpandedQuery BuildExpandedQuery(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText) || ContainsExplicitWildcards(searchText))
        {
            return default;
        }

        var parsedTokens = ParseTokens(searchText);
        if (parsedTokens.Count == 0 || parsedTokens.Any(static token => token.Kind == ParsedTokenKind.ComplexSyntax))
        {
            return default;
        }

        var rawTerms = parsedTokens
            .Where(static token => token.Kind == ParsedTokenKind.PlainTextTerm)
            .Select(static token => token.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rawTerms.Count == 0)
        {
            return default;
        }

        var structuredTokens = parsedTokens
            .Where(static token => token.Kind == ParsedTokenKind.StructuredToken)
            .Select(static token => token.Value)
            .ToList();

        var structuredSearchText = structuredTokens.Count > 0
            ? string.Join(' ', structuredTokens)
            : null;

        var containsRestriction = BuildContainsRestriction(ExtractContainsTerms(rawTerms));
        var likeRestriction = BuildLikeRestriction(rawTerms);
        var primaryRestriction = CombineRestrictions(containsRestriction, likeRestriction);

        if (string.IsNullOrWhiteSpace(primaryRestriction))
        {
            return default;
        }

        var fallbackRestriction = !string.IsNullOrWhiteSpace(containsRestriction) && !string.IsNullOrWhiteSpace(likeRestriction)
            ? likeRestriction
            : null;

        return new ImplicitWildcardExpandedQuery(
            structuredSearchText,
            primaryRestriction,
            fallbackRestriction);
    }

    private static List<ParsedToken> ParseTokens(string searchText)
    {
        var parsedTokens = new List<ParsedToken>();
        var seenTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expectsStructuredValue = false;

        foreach (var token in Tokenize(searchText))
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (IsComplexSyntaxToken(token))
            {
                parsedTokens.Add(new ParsedToken(token, ParsedTokenKind.ComplexSyntax));
                expectsStructuredValue = false;
                continue;
            }

            if (expectsStructuredValue || IsStructuredToken(token))
            {
                parsedTokens.Add(new ParsedToken(token, ParsedTokenKind.StructuredToken));
                expectsStructuredValue = ExpectsAnotherStructuredValue(token);
                continue;
            }

            var candidate = Unquote(token).Trim();
            if (candidate.Length == 0 || !ContainsSearchableCharacters(candidate))
            {
                expectsStructuredValue = false;
                continue;
            }

            if (seenTerms.Add(candidate))
            {
                parsedTokens.Add(new ParsedToken(candidate, ParsedTokenKind.PlainTextTerm));
            }

            expectsStructuredValue = false;
        }

        return parsedTokens;
    }

    private static bool ContainsExplicitWildcards(string searchText)
    {
        return searchText.Contains('*') || searchText.Contains('?');
    }

    private static List<string> Tokenize(string searchText)
    {
        var tokens = new List<string>();
        var currentToken = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in searchText)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                currentToken.Append(ch);
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                AppendCurrentToken(tokens, currentToken);
                continue;
            }

            currentToken.Append(ch);
        }

        AppendCurrentToken(tokens, currentToken);
        return tokens;
    }

    private static void AppendCurrentToken(List<string> tokens, StringBuilder currentToken)
    {
        if (currentToken.Length == 0)
        {
            return;
        }

        tokens.Add(currentToken.ToString());
        currentToken.Clear();
    }

    private static bool IsStructuredToken(string token)
    {
        if (token.Length > 0 && token[0] is '+' or '-')
        {
            return true;
        }

        if (token.Contains('\\') || token.Contains('/'))
        {
            return true;
        }

        if (token.Contains('=') || token.Contains('>') || token.Contains('<'))
        {
            return true;
        }

        return token.Contains(':') && !LooksLikeDrivePath(token);
    }

    private static bool ExpectsAnotherStructuredValue(string token)
    {
        if (!token.Contains(':') || LooksLikeDrivePath(token))
        {
            return false;
        }

        var suffix = token[(token.LastIndexOf(':') + 1)..];
        return suffix.Length == 0 || suffix.Equals("NOT", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsComplexSyntaxToken(string token)
    {
        return token.Contains('(')
               || token.Contains(')')
               || IsBooleanOperator(token);
    }

    private static bool IsBooleanOperator(string token)
    {
        return token.Equals("AND", StringComparison.OrdinalIgnoreCase)
               || token.Equals("OR", StringComparison.OrdinalIgnoreCase)
               || token.Equals("NOT", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeDrivePath(string token)
    {
        return token.Length >= 2
               && char.IsLetter(token[0])
               && token[1] == ':'
               && (token.Length == 2 || token[2] is '\\' or '/');
    }

    private static bool ContainsSearchableCharacters(string token)
    {
        foreach (var ch in token)
        {
            if (char.IsLetterOrDigit(ch) || IsLiteralLikeSearchCharacter(ch))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLiteralLikeSearchCharacter(char ch)
    {
        return ch is '%' or '_';
    }

    private static string Unquote(string token)
    {
        return token switch
        {
            ['"', .. var inner, '"'] => inner,
            _ => token,
        };
    }

    private static List<string> ExtractContainsTerms(IReadOnlyList<string> rawTerms)
    {
        var terms = new List<string>();
        var seenTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawTerm in rawTerms)
        {
            foreach (var candidate in ExtractContainsTermCandidates(rawTerm))
            {
                if (candidate.Length < MinimumContainsTermLength)
                {
                    continue;
                }

                if (seenTerms.Add(candidate))
                {
                    terms.Add(candidate);
                }
            }
        }

        return terms;
    }

    private static IEnumerable<string> ExtractContainsTermCandidates(string rawTerm)
    {
        if (ShouldUseLiteralOnlyMatching(rawTerm))
        {
            return [];
        }

        var normalized = new StringBuilder(rawTerm.Length);

        foreach (var ch in rawTerm)
        {
            normalized.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return normalized
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool ShouldUseLiteralOnlyMatching(string rawTerm)
    {
        if (rawTerm.Length < 2 || !IsWrapperPair(rawTerm[0], rawTerm[^1]))
        {
            return false;
        }

        var inner = rawTerm[1..^1];
        if (!ContainsSearchableCharacters(inner))
        {
            return false;
        }

        return !HasInternalSeparatorPunctuation(inner);
    }

    private static bool IsWrapperPair(char start, char end) =>
        (start, end) is ('[', ']') or ('{', '}') or ('<', '>');

    private static bool HasInternalSeparatorPunctuation(string value)
    {
        for (var i = 1; i < value.Length - 1; i++)
        {
            if (!char.IsLetterOrDigit(value[i]) && IsLetterOrDigitNeighbor(value, i - 1, i + 1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLetterOrDigitNeighbor(string value, int leftIndex, int rightIndex) =>
        char.IsLetterOrDigit(value[leftIndex]) && char.IsLetterOrDigit(value[rightIndex]);

    private static string? BuildContainsRestriction(IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
        {
            return null;
        }

        var predicates = new List<string>();

        if (terms.Count == 1)
        {
            predicates.Add(BuildContainsPredicate(terms[0], usePrefixWildcard: false));
            predicates.Add(BuildContainsPredicate(terms[0], usePrefixWildcard: true));
        }
        else
        {
            var phrase = string.Join(' ', terms);
            predicates.Add(BuildContainsPredicate(phrase, usePrefixWildcard: false));
            predicates.Add(BuildContainsPredicate(phrase, usePrefixWildcard: true));
            predicates.Add(BuildContainsAllTermsPredicate(terms, usePrefixWildcard: false));
            predicates.Add(BuildContainsAllTermsPredicate(terms, usePrefixWildcard: true));
        }

        return $"({string.Join(" OR ", predicates)})";
    }

    private static string BuildContainsPredicate(string term, bool usePrefixWildcard)
    {
        var escapedTerm = EscapeContainsTerm(term);
        var query = usePrefixWildcard
            ? $"\"{escapedTerm}*\""
            : $"\"{escapedTerm}\"";

        return $"CONTAINS(System.ItemNameDisplay, '{query}')";
    }

    private static string BuildContainsAllTermsPredicate(IReadOnlyList<string> terms, bool usePrefixWildcard)
    {
        var joinedTerms = string.Join(
            " AND ",
            terms.Select(term =>
            {
                var escapedTerm = EscapeContainsTerm(term);
                return usePrefixWildcard
                    ? $"\"{escapedTerm}*\""
                    : $"\"{escapedTerm}\"";
            }));

        return $"CONTAINS(System.ItemNameDisplay, '{joinedTerms}')";
    }

    private static string? BuildLikeRestriction(IReadOnlyList<string> rawTerms)
    {
        if (rawTerms.Count == 0)
        {
            return null;
        }

        var predicates = rawTerms
            .Select(BuildLikePredicate)
            .ToList();

        return predicates.Count == 1
            ? predicates[0]
            : $"({string.Join(" AND ", predicates)})";
    }

    private static string BuildLikePredicate(string term)
    {
        var escapedTerm = EscapeLikeTerm(term);
        return $"System.FileName LIKE '%{escapedTerm}%'";
    }

    private static string? CombineRestrictions(string? containsRestriction, string? likeRestriction)
    {
        if (string.IsNullOrWhiteSpace(containsRestriction))
        {
            return likeRestriction;
        }

        if (string.IsNullOrWhiteSpace(likeRestriction))
        {
            return containsRestriction;
        }

        return $"({containsRestriction} OR {likeRestriction})";
    }

    private static string EscapeContainsTerm(string value)
    {
        return value
            .Replace("'", "''", StringComparison.Ordinal)
            .Replace("\"", "\"\"", StringComparison.Ordinal);
    }

    private static string EscapeLikeTerm(string value)
    {
        var escaped = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            escaped.Append(ch switch
            {
                '[' => "[[]",
                ']' => "[]]",
                '%' => "[%]",
                '_' => "[_]",
                '\'' => "''",
                _ => ch,
            });
        }

        return escaped.ToString();
    }

    internal readonly record struct ImplicitWildcardExpandedQuery(
        string? StructuredSearchText,
        string? PrimaryRestriction,
        string? FallbackRestriction)
    {
        public bool HasPrimaryRestriction => !string.IsNullOrWhiteSpace(PrimaryRestriction);

        public bool HasFallbackRestriction => !string.IsNullOrWhiteSpace(FallbackRestriction);
    }

    private enum ParsedTokenKind
    {
        PlainTextTerm = 0,
        StructuredToken,
        ComplexSyntax,
    }

    private readonly record struct ParsedToken(string Value, ParsedTokenKind Kind);
}
