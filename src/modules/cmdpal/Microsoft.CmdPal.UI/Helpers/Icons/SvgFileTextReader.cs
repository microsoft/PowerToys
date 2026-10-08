// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class SvgFileTextReader
{
    private const int MaximumXmlDeclarationByteCount = 1024;
    private const int ReaderBufferSize = 1024;

    private static readonly Encoding Utf32LittleEndian = new UTF32Encoding(
        bigEndian: false,
        byteOrderMark: false);

    private static readonly Encoding Utf32BigEndian = new UTF32Encoding(
        bigEndian: true,
        byteOrderMark: false);

    public static bool TryRead(string path, out string text)
    {
        text = string.Empty;

        using var stream = File.OpenRead(path);

        // An XML declaration contains only version, encoding, and standalone.
        // Bound the probe so a malformed file cannot grow stack or parsing work.
        Span<byte> prefix = stackalloc byte[MaximumXmlDeclarationByteCount];
        var prefixLength = stream.ReadAtLeast(
            prefix,
            prefix.Length,
            throwOnEndOfStream: false);
        if (!TryGetEncoding(prefix[..prefixLength], out var encoding))
        {
            return false;
        }

        stream.Position = 0;
        using var reader = new StreamReader(
            stream,
            encoding,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: ReaderBufferSize);
        text = reader.ReadToEnd();
        return true;
    }

    private static bool TryGetEncoding(ReadOnlySpan<byte> prefix, out Encoding encoding)
    {
        encoding = Encoding.UTF8;

        // StreamReader handles BOMs. These signatures cover BOM-less UTF-16 and
        // UTF-32, whose zero bytes prevent reading the declaration as ASCII.
        if (prefix.Length >= 4)
        {
            if (prefix[0] == 0x00 && prefix[1] == 0x00 && prefix[2] == 0x00 && prefix[3] == 0x3C)
            {
                encoding = Utf32BigEndian;
                return true;
            }

            if (prefix[0] == 0x3C && prefix[1] == 0x00 && prefix[2] == 0x00 && prefix[3] == 0x00)
            {
                encoding = Utf32LittleEndian;
                return true;
            }

            if (prefix[0] == 0x00 && prefix[1] == 0x3C && prefix[2] == 0x00)
            {
                encoding = Encoding.BigEndianUnicode;
                return true;
            }

            if (prefix[0] == 0x3C && prefix[1] == 0x00 && prefix[3] == 0x00)
            {
                encoding = Encoding.Unicode;
                return true;
            }
        }

        if (!TryGetDeclaredEncodingName(prefix, out var encodingName))
        {
            return false;
        }

        return encodingName is null || TryResolveEncoding(encodingName, out encoding);
    }

    private static bool TryGetDeclaredEncodingName(
        ReadOnlySpan<byte> prefix,
        out string? encodingName)
    {
        encodingName = null;

        var declarationStart = 0;
        while (declarationStart < prefix.Length && IsAsciiWhitespace(prefix[declarationStart]))
        {
            declarationStart++;
        }

        if (!StartsWithXmlDeclaration(prefix[declarationStart..]))
        {
            return true;
        }

        var declarationEnd = -1;
        for (var index = declarationStart + 5; index + 1 < prefix.Length; index++)
        {
            if (prefix[index] == '?' && prefix[index + 1] == '>')
            {
                declarationEnd = index;
                break;
            }
        }

        if (declarationEnd < 0)
        {
            return false;
        }

        var declarationBytes = prefix[declarationStart..(declarationEnd + 2)];
        foreach (var value in declarationBytes)
        {
            if (value > 0x7F)
            {
                return false;
            }
        }

        var declaration = Encoding.ASCII.GetString(declarationBytes);
        var searchStart = 5;
        while (searchStart < declaration.Length)
        {
            var relativeIndex = declaration.AsSpan(searchStart)
                .IndexOf("encoding", StringComparison.OrdinalIgnoreCase);
            if (relativeIndex < 0)
            {
                return true;
            }

            var encodingIndex = searchStart + relativeIndex;
            var valueStart = encodingIndex + "encoding".Length;
            if (char.IsWhiteSpace(declaration[encodingIndex - 1])
                && (declaration[valueStart] == '=' || char.IsWhiteSpace(declaration[valueStart])))
            {
                while (valueStart < declaration.Length && char.IsWhiteSpace(declaration[valueStart]))
                {
                    valueStart++;
                }

                if (valueStart == declaration.Length || declaration[valueStart++] != '=')
                {
                    return false;
                }

                while (valueStart < declaration.Length && char.IsWhiteSpace(declaration[valueStart]))
                {
                    valueStart++;
                }

                if (valueStart == declaration.Length || declaration[valueStart] is not ('\'' or '"'))
                {
                    return false;
                }

                var quote = declaration[valueStart++];
                var valueEnd = declaration.IndexOf(quote, valueStart);
                if (valueEnd < 0 || valueEnd == valueStart)
                {
                    return false;
                }

                encodingName = declaration[valueStart..valueEnd];
                return true;
            }

            searchStart = valueStart;
        }

        return true;
    }

    private static bool TryResolveEncoding(string encodingName, out Encoding encoding)
    {
        try
        {
            encoding = Encoding.GetEncoding(encodingName);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            try
            {
                // Query the provider directly instead of changing process-wide encoding behavior.
                var codePageEncoding = CodePagesEncodingProvider.Instance.GetEncoding(encodingName);
                if (codePageEncoding is not null)
                {
                    encoding = codePageEncoding;
                    return true;
                }
            }
            catch (Exception providerException) when (providerException is ArgumentException or NotSupportedException)
            {
            }
        }

        encoding = Encoding.UTF8;
        return false;
    }

    private static bool StartsWithXmlDeclaration(ReadOnlySpan<byte> value) =>
        value.Length >= 6
        && value[0] == '<'
        && value[1] == '?'
        && ToAsciiLower(value[2]) == 'x'
        && ToAsciiLower(value[3]) == 'm'
        && ToAsciiLower(value[4]) == 'l'
        && (IsAsciiWhitespace(value[5]) || value[5] == '?');

    private static char ToAsciiLower(byte value) =>
        value is >= (byte)'A' and <= (byte)'Z'
            ? (char)(value + ('a' - 'A'))
            : (char)value;

    private static bool IsAsciiWhitespace(byte value) =>
        value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
}
