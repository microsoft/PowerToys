// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Microsoft.CmdPal.Common;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.CmdPal.Ext.Apps.Packaged;

/// <summary>Resolves Apps-owned packaged icon requests on an icon worker.</summary>
public static class PackagedAppIcons
{
    /// <summary>Identifies requests produced by the Apps catalog for host icon processing.</summary>
    public const string ProtocolPrefix = "|packaged-app-icon|";

    // ponytail: PRI managers live for the host session; evict retired package versions if churn makes this measurable.
    private static readonly ConcurrentDictionary<string, Lazy<ResourceManager>> ResourceManagers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> ReportedResourceFailures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Finds artwork for the captured rendering context, retaining package and size fallbacks.</summary>
    /// <param name="request">The immutable packaged icon request produced by the Apps catalog.</param>
    /// <param name="theme">The rendering surface's light/dark theme and captured Windows contrast qualifier.</param>
    /// <param name="targetSize">The requested pixel size.</param>
    /// <param name="path">The resolved file path, or an empty string when no artwork is available.</param>
    /// <returns>Whether usable artwork was found.</returns>
    public static bool TryResolve(string request, PackagedIconTheme theme, int targetSize, out string path)
    {
        path = string.Empty;
        if (targetSize <= 0 || !TryParse(request, out var package))
        {
            return false;
        }

        var result = AppxIconLoader.LogoPathFromUri(package.LogoUri, theme, targetSize, package);
        if (!result.MeetsMinimumSize(targetSize) && !string.IsNullOrEmpty(package.LargeLogoUri))
        {
            var alternative = AppxIconLoader.LogoPathFromUri(package.LargeLogoUri, theme, targetSize, package);
            if (alternative.IsFound)
            {
                result = alternative;
            }
        }

        path = result.LogoPath ?? string.Empty;
        return result.IsFound;
    }

    /// <summary>Creates a request whose theme is supplied by the rendering surface.</summary>
    internal static string Create(string packageFullName, string packageLocation, string logoUri, string largeLogoUri = "")
    {
        return ProtocolPrefix + string.Join('|', new[] { packageFullName, packageLocation, logoUri, largeLogoUri }.Select(Uri.EscapeDataString));
    }

    /// <summary>Reads a bounded set of resource references; malformed requests do not reach resource loading.</summary>
    internal static bool TryParse(string value, out Request request)
    {
        request = default;
        if (value?.StartsWith(ProtocolPrefix, StringComparison.Ordinal) != true)
        {
            return false;
        }

        var parts = value[ProtocolPrefix.Length..].Split('|', 5);
        if (parts.Length != 4)
        {
            return false;
        }

        try
        {
            request = new Request(
                Uri.UnescapeDataString(parts[0]),
                Uri.UnescapeDataString(parts[1]),
                Uri.UnescapeDataString(parts[2]),
                Uri.UnescapeDataString(parts[3]));
            return Path.IsPathFullyQualified(request.PackageLocation)
                && (!string.IsNullOrWhiteSpace(request.LogoUri) || !string.IsNullOrWhiteSpace(request.LargeLogoUri));
        }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException)
        {
            request = default;
            return false;
        }
    }

    /// <summary>Gets a thread-safe cached resource manager for a readable package PRI file.</summary>
    /// <returns>The usable resource manager, or null when the package has no PRI file.</returns>
    /// <remarks>Failed manager initialization is not cached and its exception propagates to the fallback resolver.</remarks>
    internal static ResourceManager? GetResourceManager(Request package)
    {
        var priPath = Path.Combine(package.PackageLocation, "resources.pri");
        if (!ResourceManagers.TryGetValue(priPath, out var manager))
        {
            // A missing index is normal for some packages and can be transient during deployment.
            if (!File.Exists(priPath))
            {
                return null;
            }

            manager = ResourceManagers.GetOrAdd(priPath, static path => new(() =>
            {
                var resources = new ResourceManager(path);

                // MRT opens the PRI lazily. Retain only a manager with a usable resource map.
                _ = resources.MainResourceMap;
                return resources;
            }));
        }

        try
        {
            return manager.Value;
        }
        catch
        {
            // A failed first read must not poison subsequent icon requests for this package.
            ResourceManagers.TryRemove(new(priPath, manager));
            throw;
        }
    }

    /// <summary>Logs the first resource-resolution failure for a package location, suppressing repeated icon warnings.</summary>
    internal static void ReportResourceFailure(Request package, Exception exception)
    {
        if (ReportedResourceFailures.TryAdd(package.PackageLocation, 0))
        {
            CoreLogger.LogWarning($"Failed to resolve icon resources for {package.PackageFullName}; using icon fallbacks: {exception}");
        }
    }

    /// <summary>Contains immutable package and logo references.</summary>
    internal readonly record struct Request(
        string PackageFullName,
        string PackageLocation,
        string LogoUri,
        string LargeLogoUri = "");
}
