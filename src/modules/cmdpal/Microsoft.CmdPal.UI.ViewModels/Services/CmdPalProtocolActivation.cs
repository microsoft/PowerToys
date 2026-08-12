// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.UI.Messages;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public sealed class CmdPalProtocolActivation : ICmdPalProtocolActivation
{
    private const string Scheme = "x-cmdpal";
    private const int MaxExtensionIdLength = 256;
    private const string GalleryPageTag = "Gallery";

    public bool TryParse(Uri? uri, [NotNullWhen(true)] out CmdPalProtocolRoute? route)
    {
        route = null;
        if (uri is null || !UriBreadcrumbs.TryParse(uri, Scheme, out var path))
        {
            return false;
        }

        route = path switch
        {
            [var background]
                when Is(background, "background")
                => new CmdPalProtocolRoute.Background(),
            [var settings]
                when Is(settings, "settings")
                => new CmdPalProtocolRoute.OpenSettings(new OpenSettingsMessage()),
            [var extensions, var gallery]
                when Is(extensions, "extensions") && Is(gallery, "gallery")
                => new CmdPalProtocolRoute.OpenSettings(new OpenSettingsMessage(GalleryPageTag)),
            [var extensions, var gallery, var extensionId]
                when Is(extensions, "extensions") && Is(gallery, "gallery") && TryParseExtensionId(extensionId, out var parsedExtensionId)
                => new CmdPalProtocolRoute.OpenSettings(new OpenSettingsMessage(GalleryPageTag, parsedExtensionId)),
            [var reload]
                when Is(reload, "reload")
                => new CmdPalProtocolRoute.Reload(),
            _ => null,
        };

        return route is not null;
    }

    private static bool Is(string actual, string expected) =>
        actual.Equals(expected, StringComparison.OrdinalIgnoreCase);

    internal bool TryParseExtensionId(string segment, out string extensionId)
    {
        extensionId = string.Empty;
        if (segment.Length is 0 or > MaxExtensionIdLength ||
            char.IsWhiteSpace(segment[0]) ||
            char.IsWhiteSpace(segment[^1]) ||
            segment.Contains('/') ||
            segment.Contains('\\') ||
            segment.Any(char.IsControl))
        {
            return false;
        }

        extensionId = segment;
        return true;
    }
}
