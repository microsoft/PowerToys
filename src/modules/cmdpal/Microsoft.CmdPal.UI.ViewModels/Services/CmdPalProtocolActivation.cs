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
    private const int MaxProviderIdLength = 256;
    private const int MaxCommandIdLength = 512;
    private const int MaxFilterIdLength = 256;
    private const int MaxQueryLength = 1024;
    private const int MaxEncodedQueryLength = 16 * 1024;
    private const string GalleryPageTag = "Gallery";
    private const string FilterParameter = "filter";
    private const string QueryParameter = "query";

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
            [var commands, var providerId, var commandId]
                when Is(commands, "commands") &&
                     TryParseCommandSegment(providerId, MaxProviderIdLength, out var parsedProviderId) &&
                     TryParseCommandSegment(commandId, MaxCommandIdLength, out var parsedCommandId) &&
                     TryParseListPageOptions(uri, out var listPageOptions)
                => new CmdPalProtocolRoute.ExecuteCommand(parsedProviderId, parsedCommandId, listPageOptions),
            _ => null,
        };

        return route is not null;
    }

    public Uri CreateUri(CmdPalProtocolRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);

        var path = route switch
        {
            CmdPalProtocolRoute.Background => "background",
            CmdPalProtocolRoute.OpenSettings { Message: { SettingsPageTag: "", ExtensionGalleryId: null } } => "settings",
            CmdPalProtocolRoute.OpenSettings { Message: { SettingsPageTag: GalleryPageTag, ExtensionGalleryId: null } } => "extensions/gallery",
            CmdPalProtocolRoute.OpenSettings { Message: { SettingsPageTag: GalleryPageTag, ExtensionGalleryId: var extensionId } }
                when TryParseExtensionId(extensionId, out var parsedExtensionId)
                => $"extensions/gallery/{Uri.EscapeDataString(parsedExtensionId)}",
            CmdPalProtocolRoute.Reload => "reload",
            CmdPalProtocolRoute.ExecuteCommand executeCommand => CreateCommandPath(executeCommand),
            _ => throw new ArgumentException("The route cannot be represented as an x-cmdpal URI.", nameof(route)),
        };

        return new Uri($"{Scheme}://{path}");
    }

    private static bool Is(string actual, string expected) =>
        actual.Equals(expected, StringComparison.OrdinalIgnoreCase);

    internal bool TryParseExtensionId(string? segment, out string extensionId) =>
        TryParsePathSegment(segment, MaxExtensionIdLength, out extensionId);

    private static bool TryParseCommandSegment(string? segment, int maxLength, out string parsed) =>
        TryParsePathSegment(segment, maxLength, out parsed);

    private static bool TryParsePathSegment(string? segment, int maxLength, out string parsed)
    {
        parsed = string.Empty;
        if (segment is null ||
            segment.Length is 0 ||
            segment.Length > maxLength ||
            char.IsWhiteSpace(segment[0]) ||
            char.IsWhiteSpace(segment[^1]) ||
            segment.Contains('/') ||
            segment.Contains('\\') ||
            segment.Any(char.IsControl))
        {
            return false;
        }

        parsed = segment;
        return true;
    }

    private static bool TryParseListPageOptions(Uri uri, out ListPageLaunchOptions? options)
    {
        options = null;
        var escapedQuery = uri.GetComponents(UriComponents.Query, UriFormat.UriEscaped);
        if (escapedQuery.Length == 0)
        {
            return true;
        }

        if (escapedQuery.Length > MaxEncodedQueryLength)
        {
            return false;
        }

        string? filterId = null;
        string? query = null;
        foreach (var escapedParameter in escapedQuery.Split('&'))
        {
            var equalsIndex = escapedParameter.IndexOf('=');
            if (equalsIndex <= 0)
            {
                return false;
            }

            var name = Uri.UnescapeDataString(escapedParameter[..equalsIndex]);
            var value = Uri.UnescapeDataString(escapedParameter[(equalsIndex + 1)..]);
            switch (name)
            {
                case FilterParameter when filterId is null:
                    if (!TryParseFilterId(value, out filterId))
                    {
                        return false;
                    }

                    break;

                case QueryParameter when query is null:
                    if (!TryParseQuery(value, out query))
                    {
                        return false;
                    }

                    break;

                default:
                    return false;
            }
        }

        options = new(query, filterId);
        return true;
    }

    private static string CreateCommandPath(CmdPalProtocolRoute.ExecuteCommand route)
    {
        if (!TryParseCommandSegment(route.ProviderId, MaxProviderIdLength, out var providerId) ||
            !TryParseCommandSegment(route.CommandId, MaxCommandIdLength, out var commandId))
        {
            throw new ArgumentException("The command route contains an invalid command identifier.", nameof(route));
        }

        var path = $"commands/{Uri.EscapeDataString(providerId)}/{Uri.EscapeDataString(commandId)}";
        if (route.ListPageOptions is null)
        {
            return path;
        }

        if (route.ListPageOptions.IsEmpty)
        {
            throw new ArgumentException("List page options must contain a query or filter.", nameof(route));
        }

        var parameters = new List<string>(2);
        if (route.ListPageOptions.FilterId is { } filterCandidate)
        {
            if (!TryParseFilterId(filterCandidate, out var filterId))
            {
                throw new ArgumentException("The command route contains an invalid filter identifier.", nameof(route));
            }

            parameters.Add($"{FilterParameter}={Uri.EscapeDataString(filterId)}");
        }

        if (route.ListPageOptions.Query is { } queryCandidate)
        {
            if (!TryParseQuery(queryCandidate, out var query))
            {
                throw new ArgumentException("The command route contains an invalid search query.", nameof(route));
            }

            parameters.Add($"{QueryParameter}={Uri.EscapeDataString(query)}");
        }

        return $"{path}?{string.Join('&', parameters)}";
    }

    private static bool TryParseFilterId(string? candidate, out string filterId)
    {
        filterId = candidate ?? string.Empty;
        return filterId.Length is > 0 and <= MaxFilterIdLength &&
               filterId.Equals(filterId.Trim(), StringComparison.Ordinal) &&
               !filterId.Any(char.IsControl);
    }

    private static bool TryParseQuery(string? candidate, out string query)
    {
        query = candidate ?? string.Empty;
        return query.Length is > 0 and <= MaxQueryLength &&
               !string.IsNullOrWhiteSpace(query) &&
               !query.Any(char.IsControl);
    }
}
