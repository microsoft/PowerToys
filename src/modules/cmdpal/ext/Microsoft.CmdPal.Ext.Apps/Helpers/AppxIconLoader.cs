// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Utils;

namespace Microsoft.CmdPal.Ext.Apps.Helpers;

internal static class AppxIconLoader
{
    private const string ContrastWhite = "contrast-white";
    private const string ContrastBlack = "contrast-black";

    private static readonly Dictionary<UWP.PackageVersion, List<int>> _scaleFactors = new()
    {
        { UWP.PackageVersion.Windows10, [100, 125, 150, 200, 400] },
        { UWP.PackageVersion.Windows81, [100, 120, 140, 160, 180] },
        { UWP.PackageVersion.Windows8, [100] },
    };

    private static readonly List<int> TargetSizes = [16, 24, 30, 36, 44, 60, 72, 96, 128, 180, 256];

    private static IconSearchResult GetScaleIcons(
        string path,
        string colorscheme,
        UWP.PackageVersion packageVersion,
        bool highContrast = false)
    {
        var extension = Path.GetExtension(path);
        if (extension is null)
        {
            return IconSearchResult.NotFound();
        }

        var end = path.Length - extension.Length;
        var prefix = path[..end];

        if (!_scaleFactors.TryGetValue(packageVersion, out var factors))
        {
            return IconSearchResult.NotFound();
        }

        var logoType = highContrast ? LogoType.HighContrast : LogoType.Colored;

        // Check from highest scale factor to lowest for best quality
        for (var i = factors.Count - 1; i >= 0; i--)
        {
            var factor = factors[i];
            string[] pathsToTry = highContrast
                ?
                [
                    $"{prefix}.scale-{factor}_{colorscheme}{extension}",
                    $"{prefix}.{colorscheme}_scale-{factor}{extension}",
                ]
                :
                [
                    $"{prefix}.scale-{factor}{extension}",
                ];

            foreach (var p in pathsToTry)
            {
                if (File.Exists(p))
                {
                    return IconSearchResult.FoundScaled(p, logoType);
                }
            }
        }

        // Check base path (100% scale) as last resort
        if (!highContrast && File.Exists(path))
        {
            return IconSearchResult.FoundScaled(path, logoType);
        }

        return IconSearchResult.NotFound();
    }

    private static IconSearchResult GetTargetSizeIcon(
        string path,
        string colorscheme,
        bool highContrast = false,
        int appIconSize = 36,
        double maxSizeCoefficient = 8.0)
    {
        var extension = Path.GetExtension(path);
        if (extension is null)
        {
            return IconSearchResult.NotFound();
        }

        var end = path.Length - extension.Length;
        var prefix = path[..end];
        var pathSizePairs = new List<(string Path, int Size)>();

        foreach (var size in TargetSizes)
        {
            if (highContrast)
            {
                pathSizePairs.Add(($"{prefix}.targetsize-{size}_{colorscheme}{extension}", size));
                pathSizePairs.Add(($"{prefix}.{colorscheme}_targetsize-{size}{extension}", size));
            }
            else
            {
                pathSizePairs.Add(($"{prefix}.targetsize-{size}_altform-unplated{extension}", size));
                pathSizePairs.Add(($"{prefix}.targetsize-{size}{extension}", size));
            }
        }

        var maxAllowedSize = (int)(appIconSize * maxSizeCoefficient);
        var logoType = highContrast ? LogoType.HighContrast : LogoType.Colored;

        string? bestLargerPath = null;
        var bestLargerSize = int.MaxValue;

        string? bestSmallerPath = null;
        var bestSmallerSize = 0;

        foreach (var (p, size) in pathSizePairs)
        {
            if (!File.Exists(p))
            {
                continue;
            }

            if (size >= appIconSize && size <= maxAllowedSize)
            {
                if (size < bestLargerSize)
                {
                    bestLargerSize = size;
                    bestLargerPath = p;
                }
            }
            else if (size < appIconSize)
            {
                if (size > bestSmallerSize)
                {
                    bestSmallerSize = size;
                    bestSmallerPath = p;
                }
            }
        }

        if (bestLargerPath is not null)
        {
            return IconSearchResult.FoundTargetSize(bestLargerPath, logoType, bestLargerSize);
        }

        if (bestSmallerPath is not null)
        {
            return IconSearchResult.FoundTargetSize(bestSmallerPath, logoType, bestSmallerSize);
        }

        return IconSearchResult.NotFound();
    }

    private static IconSearchResult GetColoredIcon(
        string path,
        string colorscheme,
        int iconSize,
        UWP package)
    {
        // First priority: targetsize icons (we know the exact size)
        var targetResult = GetTargetSizeIcon(path, colorscheme, highContrast: false, appIconSize: iconSize);
        if (targetResult.MeetsMinimumSize(iconSize))
        {
            return targetResult;
        }

        var hcTargetResult = GetTargetSizeIcon(path, colorscheme, highContrast: true, appIconSize: iconSize);
        if (hcTargetResult.MeetsMinimumSize(iconSize))
        {
            return hcTargetResult;
        }

        // Second priority: scale icons (size unknown, but higher scale = likely better)
        var scaleResult = GetScaleIcons(path, colorscheme, package.Version, highContrast: false);
        if (scaleResult.IsFound)
        {
            return scaleResult;
        }

        var hcScaleResult = GetScaleIcons(path, colorscheme, package.Version, highContrast: true);
        if (hcScaleResult.IsFound)
        {
            return hcScaleResult;
        }

        // Last resort: return undersized targetsize if we found one
        if (targetResult.IsFound)
        {
            return targetResult;
        }

        if (hcTargetResult.IsFound)
        {
            return hcTargetResult;
        }

        return IconSearchResult.NotFound();
    }

    private static IconSearchResult SetHighContrastIcon(
        string path,
        string colorscheme,
        int iconSize,
        UWP package)
    {
        // First priority: HC targetsize icons (we know the exact size)
        var hcTargetResult = GetTargetSizeIcon(path, colorscheme, highContrast: true, appIconSize: iconSize);
        if (hcTargetResult.MeetsMinimumSize(iconSize))
        {
            return hcTargetResult;
        }

        var targetResult = GetTargetSizeIcon(path, colorscheme, highContrast: false, appIconSize: iconSize);
        if (targetResult.MeetsMinimumSize(iconSize))
        {
            return targetResult;
        }

        // Second priority: scale icons
        var hcScaleResult = GetScaleIcons(path, colorscheme, package.Version, highContrast: true);
        if (hcScaleResult.IsFound)
        {
            return hcScaleResult;
        }

        var scaleResult = GetScaleIcons(path, colorscheme, package.Version, highContrast: false);
        if (scaleResult.IsFound)
        {
            return scaleResult;
        }

        // Last resort: undersized targetsize
        if (hcTargetResult.IsFound)
        {
            return hcTargetResult;
        }

        if (targetResult.IsFound)
        {
            return targetResult;
        }

        return IconSearchResult.NotFound();
    }

    /// <summary>
    /// Loads an icon from a UWP package, attempting to find the best match for the requested size.
    /// </summary>
    /// <param name="uri">The relative URI to the logo asset.</param>
    /// <param name="theme">The current theme.</param>
    /// <param name="iconSize">The requested icon size in pixels.</param>
    /// <param name="package">The UWP package.</param>
    /// <returns>
    /// An IconSearchResult. Use <see cref="IconSearchResult.MeetsMinimumSize"/> to check if
    /// the icon is confirmed to be large enough, or <see cref="IconSearchResult.IsTargetSizeIcon"/>
    /// to determine if the size is known.
    /// </returns>
    internal static IconSearchResult LogoPathFromUri(
        string uri,
        Theme theme,
        int iconSize,
        UWP package)
    {
        var path = Path.Combine(package.Location, uri);
        var logo = Probe(theme, path, iconSize, package);
        if (!logo.IsFound && !uri.Contains('\\', StringComparison.Ordinal))
        {
            path = Path.Combine(package.Location, "Assets", uri);
            logo = Probe(theme, path, iconSize, package);
        }

        return logo;
    }

    private static IconSearchResult Probe(Theme theme, string path, int iconSize, UWP package)
    {
        return theme switch
        {
            Theme.HighContrastBlack or Theme.HighContrastOne or Theme.HighContrastTwo
                => SetHighContrastIcon(path, ContrastBlack, iconSize, package),
            Theme.HighContrastWhite
                => SetHighContrastIcon(path, ContrastWhite, iconSize, package),
            Theme.Light
                => GetColoredIcon(path, ContrastWhite, iconSize, package),
            _
                => GetColoredIcon(path, ContrastBlack, iconSize, package),
        };
    }
}
