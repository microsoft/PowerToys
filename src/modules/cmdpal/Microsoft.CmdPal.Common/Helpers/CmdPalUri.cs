// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Helpers;

public static class CmdPalUri
{
    private const string Prefix = "cmdpal:";
    private const string IconPath = "icon";
    private const string NilPath = "icon/nil";
    private const string GlyphPath = "glyph";

    public static string CreateNilIcon() => Prefix + NilPath;

    public static CmdPalIconUriBuilder IconBuilder() => default;

    public static string CreateIcon(IEnumerable<CmdPalIconSourceCandidate> sources)
    {
        var queryParts = BuildCandidateQueryParts("src", "kind", sources);
        return queryParts.Count == 0
            ? CreateNilIcon()
            : $"{Prefix}{IconPath}?{string.Join("&", queryParts)}";
    }

    public static string CreateThemedIcon(
        IEnumerable<CmdPalIconSourceCandidate>? lightSources = null,
        IEnumerable<CmdPalIconSourceCandidate>? darkSources = null)
    {
        var queryParts = new List<string>();
        queryParts.AddRange(BuildCandidateQueryParts("light", "light-kind", lightSources));
        queryParts.AddRange(BuildCandidateQueryParts("dark", "dark-kind", darkSources));

        return queryParts.Count == 0
            ? CreateNilIcon()
            : $"{Prefix}{IconPath}?{string.Join("&", queryParts)}";
    }

    public static string CreateGlyph(string? glyph = null, string? fontFamily = null)
    {
        var path = string.IsNullOrEmpty(glyph)
            ? $"{Prefix}{GlyphPath}"
            : $"{Prefix}{GlyphPath}/{Escape(glyph)}";

        if (!string.IsNullOrWhiteSpace(fontFamily))
        {
            path += $"?font={Escape(fontFamily)}";
        }

        return path;
    }

    public static bool TryParse(string? iconString, out CmdPalUriInfo descriptor)
    {
        descriptor = null!;
        if (string.IsNullOrWhiteSpace(iconString) ||
            !iconString.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = iconString[Prefix.Length..];
        var queryIndex = payload.IndexOf('?');
        var path = queryIndex >= 0 ? payload[..queryIndex] : payload;
        var query = queryIndex >= 0 ? payload[(queryIndex + 1)..] : string.Empty;

        if (path.Equals(NilPath, StringComparison.OrdinalIgnoreCase))
        {
            descriptor = new(
                CmdPalUriKind.Icon,
                new CmdPalIconDescriptorInfo(
                    IsNil: true,
                    Sources: [],
                    LightSources: [],
                    DarkSources: []));
            return true;
        }

        if (path.Equals(IconPath, StringComparison.OrdinalIgnoreCase))
        {
            var sources = new List<CmdPalIconSourceCandidate>();
            var lightSources = new List<CmdPalIconSourceCandidate>();
            var darkSources = new List<CmdPalIconSourceCandidate>();
            ParseIconQuery(query.AsSpan(), sources, lightSources, darkSources);

            if (sources.Count == 0 && lightSources.Count == 0 && darkSources.Count == 0)
            {
                return false;
            }

            descriptor = new(
                CmdPalUriKind.Icon,
                new CmdPalIconDescriptorInfo(
                    IsNil: false,
                    Sources: sources,
                    LightSources: lightSources,
                    DarkSources: darkSources));
            return true;
        }

        if (path.Equals(GlyphPath, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(GlyphPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            var glyph = path.Length == GlyphPath.Length
                ? string.Empty
                : Unescape(path[(GlyphPath.Length + 1)..]);

            var fontFamily = ParseGlyphFontFamily(query.AsSpan());

            descriptor = new(
                CmdPalUriKind.Glyph,
                Glyph: glyph,
                FontFamily: fontFamily);
            return true;
        }

        return false;
    }

    private static List<string> BuildCandidateQueryParts(
        string sourceKey,
        string kindKey,
        IEnumerable<CmdPalIconSourceCandidate>? sources)
    {
        var queryParts = new List<string>();
        if (sources is null)
        {
            return queryParts;
        }

        foreach (var source in sources)
        {
            var normalizedSource = NormalizeNestedUri(source.Source);
            if (string.IsNullOrWhiteSpace(normalizedSource))
            {
                continue;
            }

            queryParts.Add($"{sourceKey}={Escape(normalizedSource)}");
            if (source.Kind is not CmdPalIconSourceKind.Icon)
            {
                queryParts.Add($"{kindKey}={Escape(FormatKind(source.Kind))}");
            }
        }

        return queryParts;
    }

    private static void AddCandidate(
        List<CmdPalIconSourceCandidate> target,
        string? source,
        out List<CmdPalIconSourceCandidate>? currentCollection,
        out int currentIndex)
    {
        currentCollection = null;
        currentIndex = -1;

        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        target.Add(new CmdPalIconSourceCandidate(source, CmdPalIconSourceKind.Icon));
        currentCollection = target;
        currentIndex = target.Count - 1;
    }

    private static void ApplyKind(
        List<CmdPalIconSourceCandidate> target,
        List<CmdPalIconSourceCandidate>? currentCollection,
        int currentIndex,
        string? kind)
    {
        if (!ReferenceEquals(target, currentCollection) ||
            currentIndex < 0 ||
            currentIndex >= target.Count)
        {
            return;
        }

        target[currentIndex] = target[currentIndex] with
        {
            Kind = ParseKind(kind),
        };
    }

    private static void ParseIconQuery(
        ReadOnlySpan<char> query,
        List<CmdPalIconSourceCandidate> sources,
        List<CmdPalIconSourceCandidate> lightSources,
        List<CmdPalIconSourceCandidate> darkSources)
    {
        var currentTarget = CandidateTarget.None;
        List<CmdPalIconSourceCandidate>? currentCollection = null;
        var currentIndex = -1;
        var offset = 0;

        while (TryGetNextQueryParameter(query, ref offset, out var key, out var value))
        {
            if (EqualsParameterKey(key, "src"))
            {
                AddCandidate(sources, DecodeQueryComponent(value), out currentCollection, out currentIndex);
                currentTarget = CandidateTarget.Sources;
            }
            else if (EqualsParameterKey(key, "light"))
            {
                AddCandidate(lightSources, DecodeQueryComponent(value), out currentCollection, out currentIndex);
                currentTarget = CandidateTarget.LightSources;
            }
            else if (EqualsParameterKey(key, "dark"))
            {
                AddCandidate(darkSources, DecodeQueryComponent(value), out currentCollection, out currentIndex);
                currentTarget = CandidateTarget.DarkSources;
            }
            else if (EqualsParameterKey(key, "kind") && currentTarget == CandidateTarget.Sources)
            {
                ApplyKind(sources, currentCollection, currentIndex, DecodeQueryComponent(value));
            }
            else if (EqualsParameterKey(key, "light-kind") && currentTarget == CandidateTarget.LightSources)
            {
                ApplyKind(lightSources, currentCollection, currentIndex, DecodeQueryComponent(value));
            }
            else if (EqualsParameterKey(key, "dark-kind") && currentTarget == CandidateTarget.DarkSources)
            {
                ApplyKind(darkSources, currentCollection, currentIndex, DecodeQueryComponent(value));
            }
        }
    }

    private static string? ParseGlyphFontFamily(ReadOnlySpan<char> query)
    {
        var offset = 0;
        while (TryGetNextQueryParameter(query, ref offset, out var key, out var value))
        {
            if (EqualsParameterKey(key, "font"))
            {
                return DecodeQueryComponent(value);
            }
        }

        return null;
    }

    private static bool TryGetNextQueryParameter(
        ReadOnlySpan<char> query,
        ref int offset,
        out ReadOnlySpan<char> key,
        out ReadOnlySpan<char> value)
    {
        while (offset < query.Length)
        {
            var remainder = query[offset..];
            var separatorOffset = remainder.IndexOf('&');
            ReadOnlySpan<char> part;

            if (separatorOffset < 0)
            {
                part = remainder;
                offset = query.Length;
            }
            else
            {
                part = remainder[..separatorOffset];
                offset += separatorOffset + 1;
            }

            if (part.IsEmpty)
            {
                continue;
            }

            var equalsOffset = part.IndexOf('=');
            if (equalsOffset < 0)
            {
                key = part;
                value = ReadOnlySpan<char>.Empty;
            }
            else
            {
                key = part[..equalsOffset];
                value = part[(equalsOffset + 1)..];
            }

            return true;
        }

        key = ReadOnlySpan<char>.Empty;
        value = ReadOnlySpan<char>.Empty;
        return false;
    }

    private static bool EqualsParameterKey(ReadOnlySpan<char> key, string expected)
    {
        if (key.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ContainsEscapedCharacters(key) &&
            string.Equals(DecodeQueryComponent(key), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static CmdPalIconSourceKind ParseKind(string? kind) =>
        string.Equals(kind, "thumbnail", StringComparison.OrdinalIgnoreCase)
            ? CmdPalIconSourceKind.Thumbnail
            : CmdPalIconSourceKind.Icon;

    private static string FormatKind(CmdPalIconSourceKind kind) =>
        kind switch
        {
            CmdPalIconSourceKind.Thumbnail => "thumbnail",
            _ => "icon",
        };

    private static string NormalizeNestedUri(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        var trimmed = source.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.AbsoluteUri;
        }

        var commaIndex = trimmed.IndexOf(',');
        if (commaIndex >= 0 &&
            int.TryParse(trimmed[(commaIndex + 1)..], out _))
        {
            var pathPart = trimmed[..commaIndex];
            if (PathHelper.IsValidFilePath(pathPart))
            {
                return new Uri(Path.GetFullPath(pathPart)).AbsoluteUri + trimmed[commaIndex..];
            }
        }

        if (PathHelper.IsValidFilePath(trimmed))
        {
            return new Uri(Path.GetFullPath(trimmed)).AbsoluteUri;
        }

        return trimmed;
    }

    private static string DecodeQueryComponent(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return string.Empty;
        }

        return ContainsEscapedCharacters(value)
            ? Uri.UnescapeDataString(value.ToString())
            : value.ToString();
    }

    private static bool ContainsEscapedCharacters(ReadOnlySpan<char> value) => value.IndexOf('%') >= 0;

    private static string Escape(string value) => Uri.EscapeDataString(value ?? string.Empty);

    private static string Unescape(string value) => Uri.UnescapeDataString(value ?? string.Empty);

    private enum CandidateTarget
    {
        None,
        Sources,
        LightSources,
        DarkSources,
    }

    public struct CmdPalIconUriBuilder
    {
        private List<CmdPalIconSourceCandidate>? _sources;

        public CmdPalIconUriBuilder AddIcon(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                (_sources ??= new()).Add(new(path, CmdPalIconSourceKind.Icon));
            }

            return this;
        }

        public CmdPalIconUriBuilder AddThumbnail(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                (_sources ??= new()).Add(new(path, CmdPalIconSourceKind.Thumbnail));
            }

            return this;
        }

        public readonly string Build()
        {
            return _sources is { Count: > 0 }
                ? CreateIcon(_sources)
                : CreateNilIcon();
        }
    }
}
