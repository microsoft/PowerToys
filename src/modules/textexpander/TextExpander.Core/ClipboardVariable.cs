// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

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

/// <summary>
/// A clipboard variable: what's on the clipboard now, or with <c>:pick</c> one of the recent copies in clipboard history;
/// all of it, or one kind of part found in it. The default is {{clipboard}}. When the snippet runs, one match goes straight
/// in, and two or more ask which (see <see cref="ClipboardChoice"/>). None inserts nothing, and the rest of the snippet
/// expands (see <see cref="ClipboardGap"/>).
/// </summary>
/// <param name="PickCopy">Whether it's taken from the recent copies in clipboard history rather than what's on the clipboard now.</param>
/// <param name="Part">What's inserted.</param>
public readonly record struct ClipboardVariable(bool PickCopy = false, ClipboardPart Part = ClipboardPart.All)
{
    internal const string Name = "clipboard";
    internal const string PickName = "pick";

    /// <summary>
    /// Gets a value indicating whether the snippet may show a window to choose when it runs: a part may have two or more
    /// matches, and clipboard history two or more copies.
    /// </summary>
    public bool MayAsk => PickCopy || Part != ClipboardPart.All;

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
    /// Reads <paramref name="token"/> as one whole clipboard variable, such as <c>{{clipboard:pick:link}}</c>. Anything
    /// else, including a variable with text around it or one with a mistake that leaves it as typed, isn't one.
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

    /// <summary>Gets the variable with a part it doesn't know as the whole copy.</summary>
    public ClipboardVariable Normalized() => Enum.IsDefined(Part) ? this : this with { Part = ClipboardPart.All };

    /// <summary>Gets the variable as a snippet writes it, such as <c>{{clipboard:pick:link}}</c>.</summary>
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
        }

        return token.Append("}}").ToString();
    }

    /// <summary>
    /// Reads a token the template pattern matched, such as <c>{{clipboard:pick:link}}</c>: after the name, where the copy
    /// comes from, then the part, each after a colon. The pattern already checked how it's written.
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

        ClipboardPart part = rest switch
        {
            ":email" => ClipboardPart.Email,
            ":link" => ClipboardPart.Link,
            ":phone" => ClipboardPart.Phone,
            ":number" => ClipboardPart.Number,
            _ => ClipboardPart.All,
        };

        return new ClipboardVariable(pickCopy, part);
    }
}
