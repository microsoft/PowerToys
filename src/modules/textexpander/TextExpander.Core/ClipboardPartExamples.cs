// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Globalization;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Examples of what each part of a copy is and isn't, by the same rules as <see cref="ClipboardParts.Find"/>, which the
/// tests hold them to, so what the editor shows can't drift from what a snippet finds. Sample data only.
/// </summary>
public static class ClipboardPartExamples
{
    /// <summary>Gets the examples for <paramref name="part"/>, those that count first, with numbers as <paramref name="culture"/> writes them.</summary>
    public static IReadOnlyList<ClipboardPartExample> For(ClipboardPart part, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return part switch
        {
            ClipboardPart.Email => new ClipboardPartExample[]
            {
                new("you@example.com", Counts: true),
                new("first.last+news@mail.example.org", Counts: true),
                new("you@example", Counts: false, "AddDomainEnding"),
                new("you at example.com", Counts: false, "AddAt"),
            },
            ClipboardPart.Link => new ClipboardPartExample[]
            {
                new("https://example.com/track", Counts: true),
                new("www.example.com", Counts: true),
                new("example.com", Counts: false, "AddWebStart"),
                new("ftp://example.com/file", Counts: false, "NotWebLink"),
            },
            ClipboardPart.Phone => new ClipboardPartExample[]
            {
                new("(425) 555-0123", Counts: true),
                new("+1 425 555 0123", Counts: true),
                new("555-0123", Counts: true),
                new("4255550123", Counts: false, "AddPlusOrSpaces"),
                new("10:30, 2026-11-04", Counts: false, "TimeAndDate"),
            },
            ClipboardPart.Number => new ClipboardPartExample[]
            {
                new(SampleNumber(culture), Counts: true),
                new(2.5.ToString(culture), Counts: true),
                new("1042", Counts: true),
                new("ID123", Counts: false, "AfterLetter"),
                new("10:30, 2026-11-04", Counts: false, "TimeAndDate"),
                new("(425) 555-0123", Counts: false, "PhoneNumber"),
            },
            _ => [],
        };
    }

    /// <summary>Gets a number with a minus sign and the region's group and decimal separators, such as -1,234.5 in the US.</summary>
    public static string SampleNumber(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return (-1234.5).ToString("N1", culture);
    }
}
