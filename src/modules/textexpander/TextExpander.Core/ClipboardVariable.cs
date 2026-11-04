// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Globalization;
using System.Text;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>What a clipboard variable inserts from its copy: all of it, or only one kind of part found in it.</summary>
public enum ClipboardPart
{
    /// <summary>The whole copy, as in {{clipboard}}.</summary>
    All,

    /// <summary>An email address, as in {{clipboard:email}}.</summary>
    Email,

    /// <summary>A web link, as in {{clipboard:link}}.</summary>
    Link,

    /// <summary>A phone number, as in {{clipboard:phone}}.</summary>
    Phone,

    /// <summary>A number, as in {{clipboard:number}}.</summary>
    Number,
}

/// <summary>Which of a part's matches a clipboard variable inserts. Each different match counts once, in order.</summary>
public enum ClipboardMatch
{
    /// <summary>The first, which a part without a choice gives: {{clipboard:link}}.</summary>
    First,

    /// <summary>The one at <see cref="ClipboardVariable.Index"/>, counting from 1: {{clipboard:link:2}}.</summary>
    Numbered,

    /// <summary>The last: {{clipboard:link:last}}.</summary>
    Last,

    /// <summary>All of them, one per line: {{clipboard:link:all}}.</summary>
    All,

    /// <summary>The one the user picks when the snippet runs: {{clipboard:link:pick}}.</summary>
    Pick,
}

/// <summary>
/// A clipboard variable: what's on the clipboard now, or with <c>:pick</c> first a copy picked from clipboard history when
/// the snippet runs; all of it, or one part found in it; and for a part, which of its matches. The default is {{clipboard}}.
/// A part that isn't there inserts nothing, and the rest of the snippet expands.
/// </summary>
/// <param name="PickCopy">Whether the copy is picked from clipboard history when the snippet runs.</param>
/// <param name="Part">What's inserted from the copy.</param>
/// <param name="Match">Which of the part's matches; only for a part.</param>
/// <param name="Index">For <see cref="ClipboardMatch.Numbered"/>, which match, from 2 to <see cref="MaxIndex"/>.</param>
public readonly record struct ClipboardVariable(bool PickCopy = false, ClipboardPart Part = ClipboardPart.All, ClipboardMatch Match = ClipboardMatch.First, int Index = 0)
{
    /// <summary>The highest match a snippet can name, as in {{clipboard:link:99}}.</summary>
    public const int MaxIndex = 99;

    internal const string Name = "clipboard";
    internal const string PickName = "pick";

    /// <summary>Gets a value indicating whether the snippet may show a window to choose something when it runs.</summary>
    public bool MayAsk => PickCopy || PicksMatch;

    /// <summary>Gets a value indicating whether the user picks one of the part's matches when there's more than one.</summary>
    public bool PicksMatch => Part != ClipboardPart.All && Match == ClipboardMatch.Pick;

    /// <summary>Gets the name a part has in a snippet, such as <c>email</c>, or null for the whole copy.</summary>
    public static string? PartName(ClipboardPart part) => part switch
    {
        ClipboardPart.Email => "email",
        ClipboardPart.Link => "link",
        ClipboardPart.Phone => "phone",
        ClipboardPart.Number => "number",
        _ => null,
    };

    /// <summary>
    /// Reads <paramref name="token"/> as one whole clipboard variable, such as <c>{{clipboard:link:2}}</c>. Anything else,
    /// including a variable with text around it or one with a mistake that leaves it as typed, isn't one.
    /// </summary>
    public static bool TryParse(string? token, out ClipboardVariable variable)
    {
        variable = default;
        if (string.IsNullOrEmpty(token) || !token.StartsWith("{{" + Name, StringComparison.Ordinal))
        {
            return false;
        }

        IReadOnlyList<SnippetToken> tokens = SnippetTemplate.Parse(token);
        if (tokens.Count != 1 || tokens[0].Kind != SnippetTokenKind.Clipboard)
        {
            return false;
        }

        variable = tokens[0].Clipboard;
        return true;
    }

    /// <summary>Gets the variable in its shortest form, with what doesn't apply to it dropped: {{clipboard:link:1}} is {{clipboard:link}}.</summary>
    public ClipboardVariable Normalized()
    {
        if (Part == ClipboardPart.All || !Enum.IsDefined(Part))
        {
            return new ClipboardVariable(PickCopy);
        }

        return Match switch
        {
            ClipboardMatch.Numbered when Index >= 2 => this with { Index = Math.Min(Index, MaxIndex) },
            ClipboardMatch.Last or ClipboardMatch.All or ClipboardMatch.Pick => this with { Index = 0 },
            _ => this with { Match = ClipboardMatch.First, Index = 0 },
        };
    }

    /// <summary>Gets the variable as a snippet writes it, in its shortest form, such as <c>{{clipboard:pick:link:2}}</c>.</summary>
    public string ToToken()
    {
        ClipboardVariable variable = Normalized();
        var token = new StringBuilder("{{").Append(Name);
        if (variable.PickCopy)
        {
            token.Append(':').Append(PickName);
        }

        if (PartName(variable.Part) is { } part)
        {
            token.Append(':').Append(part);
            switch (variable.Match)
            {
                case ClipboardMatch.Numbered:
                    token.Append(':').Append(variable.Index.ToString(CultureInfo.InvariantCulture));
                    break;
                case ClipboardMatch.Last:
                    token.Append(":last");
                    break;
                case ClipboardMatch.All:
                    token.Append(":all");
                    break;
                case ClipboardMatch.Pick:
                    token.Append(':').Append(PickName);
                    break;
            }
        }

        return token.Append("}}").ToString();
    }

    /// <summary>
    /// Reads a token the template pattern matched, such as <c>{{clipboard:pick:link:2}}</c>: after the name, the copy, the
    /// part and which of its matches, each after a colon. The pattern already checked how it's written.
    /// </summary>
    internal static ClipboardVariable FromToken(ReadOnlySpan<char> token)
    {
        ReadOnlySpan<char> rest = token["{{clipboard".Length..^2];
        bool pickCopy = rest.StartsWith(":" + PickName, StringComparison.Ordinal)
            && (rest.Length == PickName.Length + 1 || rest[PickName.Length + 1] == ':');
        if (pickCopy)
        {
            rest = rest[(PickName.Length + 1)..];
        }

        ReadOnlySpan<char> part = ReadOnlySpan<char>.Empty;
        ReadOnlySpan<char> which = ReadOnlySpan<char>.Empty;
        if (rest.Length > 1)
        {
            rest = rest[1..];
            int colon = rest.IndexOf(':');
            part = colon < 0 ? rest : rest[..colon];
            which = colon < 0 ? ReadOnlySpan<char>.Empty : rest[(colon + 1)..];
        }

        ClipboardPart kind = part switch
        {
            "email" => ClipboardPart.Email,
            "link" => ClipboardPart.Link,
            "phone" => ClipboardPart.Phone,
            "number" => ClipboardPart.Number,
            _ => ClipboardPart.All,
        };

        if (kind == ClipboardPart.All)
        {
            return new ClipboardVariable(pickCopy);
        }

        return which switch
        {
            "" => new ClipboardVariable(pickCopy, kind),
            "last" => new ClipboardVariable(pickCopy, kind, ClipboardMatch.Last),
            "all" => new ClipboardVariable(pickCopy, kind, ClipboardMatch.All),
            PickName => new ClipboardVariable(pickCopy, kind, ClipboardMatch.Pick),
            _ => new ClipboardVariable(pickCopy, kind, ClipboardMatch.Numbered, int.Parse(which, NumberStyles.None, CultureInfo.InvariantCulture)).Normalized(),
        };
    }
}
