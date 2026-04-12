// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.CmdPal.Common.Helpers;

public static class IconStringParser
{
    private const string CmdPalIconPrefix = "cmdpal:icon?";

    private static readonly string[] ImageExtensions =
    [
        ".svg",
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".tiff",
        ".ico",
        ".webp",
    ];

    public static IconStringInfo Parse(string? iconString)
    {
        if (string.IsNullOrWhiteSpace(iconString))
        {
            return new(IconStringKind.Glyph, Glyph: string.Empty);
        }

        if (CmdPalUri.TryParse(iconString, out var cmdPalUri))
        {
            return cmdPalUri.Kind switch
            {
                CmdPalUriKind.Icon when cmdPalUri.Icon?.IsNil == true => new(IconStringKind.NullSource),
                CmdPalUriKind.Icon when cmdPalUri.Icon is not null => new(IconStringKind.CmdPalIcon, CmdPalIcon: cmdPalUri.Icon),
                CmdPalUriKind.Glyph => new(IconStringKind.Glyph, Glyph: cmdPalUri.Glyph ?? string.Empty, FontFamily: cmdPalUri.FontFamily),
                _ => new(IconStringKind.Glyph, Glyph: iconString),
            };
        }

        if (TryParseBinaryIcon(iconString, out var binaryPath, out var binaryIconIndex))
        {
            return new(IconStringKind.ShellIcon, BinaryPath: binaryPath, BinaryIconIndex: binaryIconIndex);
        }

        if (TryParseManagedImageUri(iconString, out var uri))
        {
            return new(IconStringKind.ImageSource, Uri: uri);
        }

        return new(IconStringKind.Glyph, Glyph: iconString);
    }

    public static bool RequiresTheme(string? iconString)
    {
        if (string.IsNullOrWhiteSpace(iconString) ||
            !iconString.StartsWith(CmdPalIconPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return iconString.Contains("light=", StringComparison.OrdinalIgnoreCase) ||
            iconString.Contains("dark=", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryParseBinaryIcon(string iconString, out string binaryPath, out int binaryIconIndex)
    {
        binaryPath = string.Empty;
        binaryIconIndex = 0;

        if (string.IsNullOrWhiteSpace(iconString))
        {
            return false;
        }

        var commaIndex = iconString.IndexOf(',');
        var candidatePath = commaIndex >= 0 ? iconString[..commaIndex] : iconString;
        candidatePath = candidatePath.Trim();

        if (!TryNormalizeBinaryPath(candidatePath, out var normalizedBinaryPath))
        {
            return false;
        }

        if (commaIndex >= 0 &&
            !int.TryParse(iconString[(commaIndex + 1)..], out binaryIconIndex))
        {
            return false;
        }

        binaryPath = normalizedBinaryPath;
        return true;
    }

    internal static bool TryParseManagedImageUri(string iconString, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;

        if (Uri.TryCreate(iconString, UriKind.Absolute, out var absoluteUri))
        {
            if (!IsLikelyManagedImageUri(absoluteUri))
            {
                return false;
            }

            uri = absoluteUri;
            return true;
        }

        if (!PathHelper.IsValidFilePath(iconString) || !HasImageExtension(iconString))
        {
            return false;
        }

        uri = new Uri(Path.GetFullPath(iconString));
        return true;
    }

    private static bool IsLikelyManagedImageUri(Uri uri)
    {
        if (!IsFileLikeUri(uri))
        {
            return true;
        }

        return HasImageExtension(uri.AbsolutePath);
    }

    private static bool IsFileLikeUri(Uri uri) =>
        uri.IsFile ||
        uri.Scheme.Equals("ms-appx", StringComparison.OrdinalIgnoreCase) ||
        uri.Scheme.Equals("ms-appdata", StringComparison.OrdinalIgnoreCase);

    private static bool TryNormalizeBinaryPath(string candidatePath, [NotNullWhen(true)] out string? normalizedBinaryPath)
    {
        normalizedBinaryPath = null;

        if (Uri.TryCreate(candidatePath, UriKind.Absolute, out var absoluteUri) &&
            (absoluteUri.IsFile || absoluteUri.Scheme.Equals("res", StringComparison.OrdinalIgnoreCase)) &&
            HasBinaryExtension(absoluteUri.LocalPath))
        {
            normalizedBinaryPath = absoluteUri.LocalPath;
            return true;
        }

        if (HasBinaryExtension(candidatePath))
        {
            normalizedBinaryPath = candidatePath;
            return true;
        }

        return false;
    }

    private static bool HasBinaryExtension(string path) =>
        path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);

    private static bool HasImageExtension(string path)
    {
        var extension = Path.GetExtension(path);
        return ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}

public enum IconStringKind
{
    NullSource,
    Glyph,
    ImageSource,
    ShellIcon,
    CmdPalIcon,
}

public readonly record struct IconStringInfo(
    IconStringKind Kind,
    Uri? Uri = null,
    string? BinaryPath = null,
    int BinaryIconIndex = 0,
    CmdPalIconDescriptorInfo? CmdPalIcon = null,
    string? Glyph = null,
    string? FontFamily = null);
