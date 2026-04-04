// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.WinGet.Models;
using Microsoft.Management.Deployment;

namespace Microsoft.CmdPal.Common.WinGet.Services;

internal static class WinGetPackageMetadataHelper
{
    private static readonly StringComparer OrdinalIgnoreCase = StringComparer.OrdinalIgnoreCase;

    public static async Task<WinGetPackageStatus> InspectPackageStatusAsync(CatalogPackage package)
    {
        try
        {
            await package.CheckInstalledStatusAsync();
            var isInstalled = package.InstalledVersion is not null;
            return new WinGetPackageStatus(
                IsInstalled: isInstalled,
                IsInstalledStateKnown: true,
                IsUpdateAvailable: isInstalled && package.IsUpdateAvailable,
                IsUpdateStateKnown: true);
        }
        catch (Exception ex)
        {
            CoreLogger.LogWarning($"Failed to inspect package status '{package.Id}': {ex.Message}");
            return new WinGetPackageStatus(
                IsInstalled: false,
                IsInstalledStateKnown: false,
                IsUpdateAvailable: false,
                IsUpdateStateKnown: false);
        }
    }

    public static WinGetPackageDetails? TryBuildPackageDetails(CatalogPackage package)
    {
        try
        {
            var defaultVersion = TryGetRef(() => package.DefaultInstallVersion);
            var installedVersion = TryGetRef(() => package.InstalledVersion);
            var packageVersion = defaultVersion ?? installedVersion;

            var packageName = ToNullIfWhiteSpace(TryGetString(() => package.Name));
            var version = packageVersion is not null ? ToNullIfWhiteSpace(TryGetString(() => packageVersion.Version)) : null;

            if (packageVersion is null)
            {
                if (packageName is null)
                {
                    return null;
                }

                return new WinGetPackageDetails(
                    Name: packageName,
                    Version: version,
                    Summary: null,
                    Description: null,
                    Publisher: null,
                    PublisherUrl: null,
                    PublisherSupportUrl: null,
                    Author: null,
                    License: null,
                    LicenseUrl: null,
                    PackageUrl: null,
                    ReleaseNotes: null,
                    ReleaseNotesUrl: null,
                    IconUrl: null,
                    DocumentationLinks: [],
                    Tags: []);
            }

            var metadata = TryGetRef(() => packageVersion.GetCatalogPackageMetadata());
            if (metadata is null)
            {
                if (packageName is null && version is null)
                {
                    return null;
                }

                return new WinGetPackageDetails(
                    Name: packageName,
                    Version: version,
                    Summary: null,
                    Description: null,
                    Publisher: null,
                    PublisherUrl: null,
                    PublisherSupportUrl: null,
                    Author: null,
                    License: null,
                    LicenseUrl: null,
                    PackageUrl: null,
                    ReleaseNotes: null,
                    ReleaseNotesUrl: null,
                    IconUrl: null,
                    DocumentationLinks: [],
                    Tags: []);
            }

            List<WinGetNamedLink> documentationLinks = [];
            var docs = TryGetRef(() => metadata.Documentations);
            if (docs is not null)
            {
                for (var i = 0; i < docs.Count; i++)
                {
                    var doc = docs[i];
                    var url = ToNullIfWhiteSpace(TryGetString(() => doc.DocumentUrl));
                    if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out _))
                    {
                        continue;
                    }

                    var label = ToNullIfWhiteSpace(TryGetString(() => doc.DocumentLabel)) ?? url;
                    documentationLinks.Add(new WinGetNamedLink(label, url));
                }
            }

            List<string> tags = [];
            var metadataTags = TryGetRef(() => metadata.Tags);
            if (metadataTags is not null)
            {
                for (var i = 0; i < metadataTags.Count; i++)
                {
                    var tag = ToNullIfWhiteSpace(metadataTags[i]);
                    if (tag is null || ContainsIgnoreCase(tags, tag))
                    {
                        continue;
                    }

                    tags.Add(tag);
                }
            }

            var iconUrl = TryResolveIconUrl(metadata);
            var summary = ToNullIfWhiteSpace(TryGetString(() => metadata.ShortDescription));
            var description = ToNullIfWhiteSpace(TryGetString(() => metadata.Description));
            var releaseNotes = ToNullIfWhiteSpace(TryGetString(() => metadata.ReleaseNotes));
            if (releaseNotes is not null && releaseNotes.Length > 800)
            {
                releaseNotes = string.Concat(releaseNotes.AsSpan(0, 800), "...");
            }

            var details = new WinGetPackageDetails(
                Name: ToNullIfWhiteSpace(TryGetString(() => metadata.PackageName)) ?? packageName,
                Version: version,
                Summary: summary,
                Description: description,
                Publisher: ToNullIfWhiteSpace(TryGetString(() => metadata.Publisher)),
                PublisherUrl: ValidateAbsoluteUri(TryGetString(() => metadata.PublisherUrl)),
                PublisherSupportUrl: ValidateAbsoluteUri(TryGetString(() => metadata.PublisherSupportUrl)),
                Author: ToNullIfWhiteSpace(TryGetString(() => metadata.Author)),
                License: ToNullIfWhiteSpace(TryGetString(() => metadata.License)),
                LicenseUrl: ValidateAbsoluteUri(TryGetString(() => metadata.LicenseUrl)),
                PackageUrl: ValidateAbsoluteUri(TryGetString(() => metadata.PackageUrl)),
                ReleaseNotes: releaseNotes,
                ReleaseNotesUrl: ValidateAbsoluteUri(TryGetString(() => metadata.ReleaseNotesUrl)),
                IconUrl: iconUrl,
                DocumentationLinks: documentationLinks,
                Tags: tags);

            return HasDetailsContent(details) ? details : null;
        }
        catch (Exception ex)
        {
            CoreLogger.LogWarning($"Failed to build package metadata: {ex.Message}");
            return null;
        }
    }

    public static string GetPackageDisplayName(CatalogPackage package)
    {
        var name = ToNullIfWhiteSpace(TryGetString(() => package.Name));
        return name ?? package.Id;
    }

    public static WinGetExtensionCatalogEntry CreateExtensionCatalogEntry(CatalogPackage package)
    {
        var details = TryBuildPackageDetails(package);
        var packageName = details?.Name ?? GetPackageDisplayName(package);

        return new WinGetExtensionCatalogEntry(
            PackageId: package.Id,
            PackageName: packageName,
            Summary: details?.Summary,
            Description: details?.Description,
            Publisher: details?.Publisher,
            PublisherUrl: details?.PublisherUrl,
            Author: details?.Author,
            PackageUrl: details?.PackageUrl ?? details?.PublisherSupportUrl ?? details?.PublisherUrl,
            IconUrl: details?.IconUrl,
            Tags: details?.Tags ?? []);
    }

    private static bool HasDetailsContent(WinGetPackageDetails details)
    {
        return !string.IsNullOrWhiteSpace(details.Name)
            || !string.IsNullOrWhiteSpace(details.Version)
            || !string.IsNullOrWhiteSpace(details.Summary)
            || !string.IsNullOrWhiteSpace(details.Description)
            || !string.IsNullOrWhiteSpace(details.Publisher)
            || !string.IsNullOrWhiteSpace(details.PublisherUrl)
            || !string.IsNullOrWhiteSpace(details.PublisherSupportUrl)
            || !string.IsNullOrWhiteSpace(details.Author)
            || !string.IsNullOrWhiteSpace(details.License)
            || !string.IsNullOrWhiteSpace(details.LicenseUrl)
            || !string.IsNullOrWhiteSpace(details.PackageUrl)
            || !string.IsNullOrWhiteSpace(details.ReleaseNotes)
            || !string.IsNullOrWhiteSpace(details.ReleaseNotesUrl)
            || !string.IsNullOrWhiteSpace(details.IconUrl)
            || details.DocumentationLinks.Count > 0
            || details.Tags.Count > 0;
    }

    private static string? TryResolveIconUrl(CatalogPackageMetadata metadata)
    {
        var icons = TryGetRef(() => metadata.Icons);
        if (icons is null)
        {
            return null;
        }

        for (var i = 0; i < icons.Count; i++)
        {
            var icon = icons[i];
            var url = ValidateAbsoluteUri(TryGetString(() => icon.Url));
            if (url is not null)
            {
                return url;
            }
        }

        return null;
    }

    private static T? TryGetRef<T>(Func<T> getter)
        where T : class
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetString(Func<string> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static string? ValidateAbsoluteUri(string? value)
    {
        var normalized = ToNullIfWhiteSpace(value);
        if (normalized is null || !Uri.TryCreate(normalized, UriKind.Absolute, out _))
        {
            return null;
        }

        return normalized;
    }

    private static string? ToNullIfWhiteSpace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static bool ContainsIgnoreCase(IReadOnlyList<string> values, string candidate)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (OrdinalIgnoreCase.Equals(values[i], candidate))
            {
                return true;
            }
        }

        return false;
    }
}
