// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class ShellItemIconRequestClassifier
{
    public static bool TryClassify(string? value, out ShellItemIconRequest request)
    {
        request = default;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value[0] == '|')
        {
            if (ShellItemIconProtocol.TryParse(value, out var protocolPath, out var jumbo))
            {
                request = new ShellItemIconRequest(value, protocolPath, jumbo);
                return true;
            }

            // Protocol values, including malformed Shell-item requests, must be handled
            // by the protocol registry and must not fall through into path detection.
            return false;
        }

        try
        {
            if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(value, UriKind.Absolute, out var fileUri)
                    && fileUri.IsFile
                    && !IsDirectImagePath(fileUri.LocalPath))
                {
                    request = new ShellItemIconRequest(value, fileUri.LocalPath, false);
                    return true;
                }

                return false;
            }

            // The dominant icon path is a one- or two-character glyph. Reject anything
            // that cannot be a drive-qualified or UNC path before calling Path helpers.
            if (!MightBeFullyQualifiedPath(value)
                || !Path.IsPathFullyQualified(value)
                || IsDirectImagePath(value)
                || LooksLikeBinaryIconReference(value))
            {
                return false;
            }

            request = new ShellItemIconRequest(value, jumbo: false);
            return true;
        }
        catch
        {
            // Treat malformed legacy strings exactly as the ordinary converter does.
            // Classification is an optimization boundary and must not fail a UI request.
            request = default;
            return false;
        }
    }

    private static bool MightBeFullyQualifiedPath(string value) =>
        (value.Length >= 3 && value[1] == ':' && IsDirectorySeparator(value[2]))
        || (value.Length >= 2 && IsDirectorySeparator(value[0]) && IsDirectorySeparator(value[1]));

    private static bool IsDirectorySeparator(char value) => value is '\\' or '/';

    private static bool LooksLikeBinaryIconReference(string value)
    {
        var path = value.AsSpan();
        var commaIndex = path.IndexOf(',');
        if (commaIndex >= 0)
        {
            path = path[..commaIndex];
        }

        return path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDirectImagePath(string path) => ThumbnailHelper.IsImagePath(path);
}
