// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.CmdPal.Common;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Win32;

namespace Microsoft.CmdPal.Ext.Apps.Helpers;

internal static class AppxIconLoader
{
    private const string ContrastWhite = "white";
    private const string ContrastBlack = "black";
    private const int MaxTargetSizeCoefficient = 8;

    private static IconSearchResult GetTargetSizeIcon(
        IReadOnlyList<IconCandidate> candidates,
        PackagedIconTheme theme,
        string desiredContrast,
        bool highContrast,
        int appIconSize,
        IconCandidate? resolvedCandidate)
    {
        IconCandidate? bestCandidate = null;
        var maxAllowedSize = appIconSize > int.MaxValue / MaxTargetSizeCoefficient
            ? int.MaxValue
            : appIconSize * MaxTargetSizeCoefficient;

        foreach (var candidate in candidates)
        {
            if (candidate.TargetSize is not int targetSize ||
                candidate.IsHighContrast != highContrast ||
                targetSize > maxAllowedSize)
            {
                continue;
            }

            if (bestCandidate is null ||
                IsBetterTargetSizeCandidate(
                    candidate,
                    bestCandidate,
                    appIconSize,
                    theme,
                    desiredContrast,
                    resolvedCandidate))
            {
                bestCandidate = candidate;
            }
        }

        return bestCandidate is null
            ? IconSearchResult.NotFound()
            : IconSearchResult.FoundTargetSize(
                bestCandidate.Path,
                bestCandidate.IsHighContrast,
                bestCandidate.TargetSize!.Value);
    }

    private static bool IsBetterTargetSizeCandidate(
        IconCandidate candidate,
        IconCandidate current,
        int requestedSize,
        PackagedIconTheme theme,
        string desiredContrast,
        IconCandidate? resolvedCandidate)
    {
        var comparison = GetPreference(candidate, theme, desiredContrast, resolvedCandidate)
            .CompareTo(GetPreference(current, theme, desiredContrast, resolvedCandidate));
        if (comparison != 0)
        {
            return comparison > 0;
        }

        var candidateSize = candidate.TargetSize!.Value;
        var currentSize = current.TargetSize!.Value;
        var candidateMeetsSize = candidateSize >= requestedSize;
        var currentMeetsSize = currentSize >= requestedSize;

        if (candidateMeetsSize != currentMeetsSize)
        {
            return candidateMeetsSize;
        }

        if (candidateSize != currentSize)
        {
            return candidateMeetsSize
                ? candidateSize < currentSize
                : candidateSize > currentSize;
        }

        return string.Compare(candidate.Path, current.Path, StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static IconSearchResult GetScaleIcon(
        IReadOnlyList<IconCandidate> candidates,
        PackagedIconTheme theme,
        string desiredContrast,
        bool highContrast,
        IconCandidate? resolvedCandidate)
    {
        IconCandidate? bestCandidate = null;

        foreach (var candidate in candidates)
        {
            if (candidate.TargetSize is not null || candidate.IsHighContrast != highContrast)
            {
                continue;
            }

            if (bestCandidate is null ||
                IsBetterScaleCandidate(candidate, bestCandidate, theme, desiredContrast, resolvedCandidate))
            {
                bestCandidate = candidate;
            }
        }

        return bestCandidate is null
            ? IconSearchResult.NotFound()
            : IconSearchResult.FoundScaled(bestCandidate.Path, bestCandidate.IsHighContrast);
    }

    private static bool IsBetterScaleCandidate(
        IconCandidate candidate,
        IconCandidate current,
        PackagedIconTheme theme,
        string desiredContrast,
        IconCandidate? resolvedCandidate)
    {
        var comparison = GetPreference(candidate, theme, desiredContrast, resolvedCandidate)
            .CompareTo(GetPreference(current, theme, desiredContrast, resolvedCandidate));
        if (comparison != 0)
        {
            return comparison > 0;
        }

        comparison = (candidate.Scale ?? 0).CompareTo(current.Scale ?? 0);
        if (comparison != 0)
        {
            return comparison > 0;
        }

        return string.Compare(candidate.Path, current.Path, StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static int GetContextScore(IconCandidate candidate, IconCandidate? resolvedCandidate)
    {
        if (resolvedCandidate is null)
        {
            var unresolvedContextScore = 0;
            foreach (var qualifier in candidate.Qualifiers)
            {
                if (!IsSelectionQualifier(qualifier.Key))
                {
                    unresolvedContextScore--;
                }
            }

            return unresolvedContextScore;
        }

        var score = 0;
        foreach (var qualifier in resolvedCandidate.Qualifiers)
        {
            if (IsSelectionQualifier(qualifier.Key))
            {
                continue;
            }

            if (!candidate.Qualifiers.TryGetValue(qualifier.Key, out var candidateValue))
            {
                score--;
            }
            else if (string.Equals(candidateValue, qualifier.Value, StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }
            else
            {
                score -= 1000;
            }
        }

        foreach (var qualifier in candidate.Qualifiers)
        {
            if (!IsSelectionQualifier(qualifier.Key) &&
                !resolvedCandidate.Qualifiers.ContainsKey(qualifier.Key))
            {
                score--;
            }
        }

        return score;
    }

    private static bool IsContextCompatible(IconCandidate candidate, IconCandidate? resolvedCandidate)
    {
        if (resolvedCandidate is null)
        {
            return true;
        }

        foreach (var qualifier in candidate.Qualifiers)
        {
            if (!IsSelectionQualifier(qualifier.Key) &&
                resolvedCandidate.Qualifiers.TryGetValue(qualifier.Key, out var resolvedValue) &&
                !string.Equals(qualifier.Value, resolvedValue, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSelectionQualifier(string qualifierName)
    {
        return qualifierName is "scale" or "targetsize" or "contrast" or "theme" or "altform";
    }

    private static int GetContrastScore(IconCandidate candidate, string desiredContrast)
    {
        if (!candidate.IsHighContrast)
        {
            return 0;
        }

        if (string.Equals(candidate.Contrast, desiredContrast, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (string.Equals(candidate.Contrast, "high", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 1;
    }

    private static int GetThemeScore(IconCandidate candidate, PackagedIconTheme theme)
    {
        if (candidate.Theme is null)
        {
            return 1;
        }

        var desiredTheme = theme.IsLight ? "light" : "dark";
        return string.Equals(candidate.Theme, desiredTheme, StringComparison.OrdinalIgnoreCase) ? 2 : 0;
    }

    private static int GetAlternateFormScore(IconCandidate candidate, PackagedIconTheme theme)
    {
        if (candidate.AlternateForm is null)
        {
            return 1;
        }

        if (theme.IsLight)
        {
            if (string.Equals(candidate.AlternateForm, "lightunplated", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            return string.Equals(candidate.AlternateForm, "unplated", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        }

        if (string.Equals(candidate.AlternateForm, "unplated", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return string.Equals(candidate.AlternateForm, "lightunplated", StringComparison.OrdinalIgnoreCase) ? 0 : 2;
    }

    private static (int Context, int Contrast, int Theme, int AlternateForm) GetPreference(
        IconCandidate candidate,
        PackagedIconTheme theme,
        string desiredContrast,
        IconCandidate? resolvedCandidate)
    {
        return (
            GetContextScore(candidate, resolvedCandidate),
            GetContrastScore(candidate, desiredContrast),
            GetThemeScore(candidate, theme),
            GetAlternateFormScore(candidate, theme));
    }

    private static IconSearchResult GetIcon(
        IReadOnlyList<IconCandidate> candidates,
        PackagedIconTheme theme,
        string desiredContrast,
        bool highContrast,
        int iconSize,
        IconCandidate? resolvedCandidate)
    {
        // Prefer usable artwork of the requested contrast before opposite-contrast assets.
        var targetResult = GetTargetSizeIcon(candidates, theme, desiredContrast, highContrast, iconSize, resolvedCandidate);
        if (targetResult.MeetsMinimumSize(iconSize))
        {
            return targetResult;
        }

        var scaleResult = GetScaleIcon(candidates, theme, desiredContrast, highContrast, resolvedCandidate);
        if (scaleResult.IsFound)
        {
            return scaleResult;
        }

        var alternateTargetResult = GetTargetSizeIcon(candidates, theme, desiredContrast, !highContrast, iconSize, resolvedCandidate);
        if (alternateTargetResult.MeetsMinimumSize(iconSize))
        {
            return alternateTargetResult;
        }

        var alternateScaleResult = GetScaleIcon(candidates, theme, desiredContrast, !highContrast, resolvedCandidate);
        if (alternateScaleResult.IsFound)
        {
            return alternateScaleResult;
        }

        // Last resort: return an undersized targetsize candidate.
        return targetResult.IsFound ? targetResult : alternateTargetResult;
    }

    private static (bool NotUndersized, bool MatchesContrast, bool MeetsMinimumSize) GetResultPreference(
        IconSearchResult result,
        PackagedIconTheme theme,
        int iconSize)
    {
        return (
            !result.IsKnownUndersized(iconSize),
            result.IsHighContrast == theme.IsHighContrast,
            result.MeetsMinimumSize(iconSize));
    }

    /// <summary>
    /// Finds packaged artwork that best matches the requested rendering context and size.
    /// </summary>
    /// <param name="uri">The logical package-relative URI to the logo asset.</param>
    /// <param name="theme">The captured surface theme and system contrast qualifier.</param>
    /// <param name="iconSize">The requested icon size in pixels.</param>
    /// <param name="package">The immutable package identity and installation location.</param>
    /// <returns>
    /// An IconSearchResult. Use <see cref="IconSearchResult.MeetsMinimumSize"/> to check if
    /// the icon is confirmed to be large enough, or <see cref="IconSearchResult.IsTargetSizeIcon"/>
    /// to determine if the size is known.
    /// </returns>
    internal static IconSearchResult LogoPathFromUri(
        string uri,
        PackagedIconTheme theme,
        int iconSize,
        PackagedAppIcons.Request package)
    {
        var resolvedResourcePath = TryResolvePackageResourcePath(uri, theme, iconSize, package);

        // PRI can return undersized or plated artwork even with explicit qualifiers.
        // Keep filename selection for size, alternate-form and split-resource fallbacks.
        return LogoPathFromUri(uri, theme, iconSize, package, resolvedResourcePath);
    }

    /// <summary>Finds context-compatible logo assets using a previously resolved PRI candidate and package-local fallbacks.</summary>
    /// <param name="uri">The logical package-relative logo URI.</param>
    /// <param name="theme">The rendering surface theme and captured contrast qualifier.</param>
    /// <param name="iconSize">The requested icon size in pixels.</param>
    /// <param name="package">The immutable package identity and installation location.</param>
    /// <param name="resolvedResourcePath">A PRI-selected candidate, or null when no resource index result is available.</param>
    internal static IconSearchResult LogoPathFromUri(
        string uri,
        PackagedIconTheme theme,
        int iconSize,
        PackagedAppIcons.Request package,
        string? resolvedResourcePath)
    {
        if (string.IsNullOrWhiteSpace(uri) || iconSize <= 0)
        {
            return IconSearchResult.NotFound();
        }

        var pathsToProbe = new List<string>();
        if (!string.IsNullOrEmpty(resolvedResourcePath))
        {
            var resolvedDirectory = Path.GetDirectoryName(resolvedResourcePath);
            var logicalFileName = Path.GetFileName(NormalizePathSeparators(uri));
            if (!string.IsNullOrEmpty(resolvedDirectory) && !string.IsNullOrEmpty(logicalFileName))
            {
                AddPathIfUnique(pathsToProbe, Path.Combine(resolvedDirectory, logicalFileName));
            }
        }

        var relativePath = NormalizePathSeparators(uri).TrimStart(Path.DirectorySeparatorChar);
        AddPathIfUnique(pathsToProbe, Path.Combine(package.PackageLocation, relativePath));
        AddPathIfUnique(pathsToProbe, Path.Combine(package.PackageLocation, "Assets", relativePath));
        AddPathIfUnique(pathsToProbe, Path.Combine(package.PackageLocation, "Images", relativePath));

        var fallback = IconSearchResult.NotFound();
        foreach (var path in pathsToProbe)
        {
            var result = Probe(theme, path, iconSize, resolvedResourcePath);
            if (!result.IsFound)
            {
                continue;
            }

            var preference = GetResultPreference(result, theme, iconSize);
            if (preference.MatchesContrast && preference.MeetsMinimumSize)
            {
                return result;
            }

            // Keep the first equally suitable fallback so PRI scale assets stay preferred.
            if (!fallback.IsFound ||
                preference.CompareTo(GetResultPreference(fallback, theme, iconSize)) > 0)
            {
                fallback = result;
            }
        }

        return fallback.IsFound ? fallback : GetResolvedResourceFallback(resolvedResourcePath);
    }

    private static void AddPathIfUnique(List<string> paths, string path)
    {
        foreach (var existingPath in paths)
        {
            if (string.Equals(existingPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        paths.Add(path);
    }

    private static string NormalizePathSeparators(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static string? TryResolvePackageResourcePath(
        string uri,
        PackagedIconTheme theme,
        int iconSize,
        PackagedAppIcons.Request package)
    {
        if (string.IsNullOrWhiteSpace(uri) ||
            string.IsNullOrWhiteSpace(package.PackageFullName))
        {
            return null;
        }

        // Package identity names cannot contain the underscore separating full-name fields.
        var nameEnd = package.PackageFullName.IndexOf('_');
        if (nameEnd <= 0)
        {
            return null;
        }

        var packageName = package.PackageFullName[..nameEnd];

        // Manifest asset paths are logical resource names. PRI resolution accounts for
        // physical resource roots and qualifiers stored in folders, such as "images" or "en-US".
        var normalizedResourcePath = uri.Replace('\\', '/').TrimStart('/');
        var pathSegments = normalizedResourcePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathSegments.Length == 0)
        {
            return null;
        }

        try
        {
            // Override the resource context for forced surface themes and capture contrast
            // with the request. Qualifier folders need PRI selection as well as filename ranking.
            var resources = PackagedAppIcons.GetResourceManager(package);
            if (resources is not null)
            {
                var context = resources.CreateResourceContext();
                context.QualifierValues["Theme"] = theme.IsLight ? "light" : "dark";
                context.QualifierValues["Contrast"] = GetContrastQualifier(theme);
                context.QualifierValues["TargetSize"] = iconSize.ToString(CultureInfo.InvariantCulture);
                context.QualifierValues["AlternateForm"] = theme.IsLight ? "lightunplated" : "unplated";
                var candidate = resources.MainResourceMap.TryGetValue("Files/" + normalizedResourcePath, context);
                if (candidate?.Kind == ResourceCandidateKind.FilePath)
                {
                    var candidatePath = candidate.ValueAsString;
                    if (!Path.IsPathFullyQualified(candidatePath))
                    {
                        candidatePath = Path.Combine(package.PackageLocation, candidatePath);
                    }

                    if (File.Exists(candidatePath))
                    {
                        return candidatePath;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            // Older/split resource indexes may require the installed-package Shell resolver.
            PackagedAppIcons.ReportResourceFailure(package, exception);
        }

        for (var i = 0; i < pathSegments.Length; i++)
        {
            pathSegments[i] = Uri.EscapeDataString(pathSegments[i]);
        }

        var resourceUri = $"ms-resource://{packageName}/Files/{string.Join('/', pathSegments)}";
        var source = $"@{{{package.PackageFullName}?{resourceUri}}}";
        Span<char> output = stackalloc char[1024];
        var result = PInvoke.SHLoadIndirectString(source, output);
        if (result.Failed)
        {
            return null;
        }

        var terminatorIndex = output.IndexOf('\0');
        var resolvedPath = terminatorIndex >= 0
            ? output[..terminatorIndex].ToString()
            : output.ToString();

        return File.Exists(resolvedPath) ? resolvedPath : null;
    }

    private static IconSearchResult Probe(
        PackagedIconTheme theme,
        string path,
        int iconSize,
        string? resolvedResourcePath)
    {
        var candidates = FindCandidates(path);
        if (candidates.Count == 0)
        {
            return IconSearchResult.NotFound();
        }

        IconCandidate? resolvedCandidate = null;
        if (!string.IsNullOrEmpty(resolvedResourcePath))
        {
            TryCreateCandidate(path, resolvedResourcePath, out resolvedCandidate);
        }

        // Size variants must preserve the language and configuration selected by PRI.
        candidates.RemoveAll(candidate => !IsContextCompatible(candidate, resolvedCandidate));

        var desiredContrast = theme.IsHighContrast ? GetContrastQualifier(theme)
            : theme.IsLight ? ContrastWhite : ContrastBlack;
        return GetIcon(candidates, theme, desiredContrast, theme.IsHighContrast, iconSize, resolvedCandidate);
    }

    private static string GetContrastQualifier(PackagedIconTheme theme)
    {
        return theme.Contrast switch
        {
            IconContrastMode.White => ContrastWhite,
            IconContrastMode.Black => ContrastBlack,
            IconContrastMode.High => "high",
            _ => "standard",
        };
    }

    private static List<IconCandidate> FindCandidates(string path)
    {
        var candidates = new List<IconCandidate>();
        var directory = Path.GetDirectoryName(path);
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(directory) ||
            string.IsNullOrEmpty(extension) ||
            !Directory.Exists(directory))
        {
            return candidates;
        }

        try
        {
            foreach (var candidatePath in Directory.EnumerateFiles(directory))
            {
                if (TryCreateCandidate(path, candidatePath, out var candidate) && candidate is not null)
                {
                    candidates.Add(candidate);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Resource resolution is best-effort. The caller will try the next package path.
        }

        return candidates;
    }

    private static bool TryCreateCandidate(
        string logicalBasePath,
        string candidatePath,
        out IconCandidate? candidate)
    {
        candidate = null;

        var expectedExtension = Path.GetExtension(logicalBasePath);
        if (string.IsNullOrEmpty(expectedExtension) ||
            !string.Equals(Path.GetExtension(candidatePath), expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var logicalBaseName = Path.GetFileNameWithoutExtension(logicalBasePath);
        var candidateBaseName = Path.GetFileNameWithoutExtension(candidatePath);
        string qualifierList;

        if (string.Equals(candidateBaseName, logicalBaseName, StringComparison.OrdinalIgnoreCase))
        {
            qualifierList = string.Empty;
        }
        else
        {
            var qualifiedPrefix = logicalBaseName + ".";
            if (!candidateBaseName.StartsWith(qualifiedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            qualifierList = candidateBaseName[qualifiedPrefix.Length..];
        }

        var qualifiers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(qualifierList))
        {
            foreach (var qualifier in qualifierList.Split('_', StringSplitOptions.RemoveEmptyEntries))
            {
                var separatorIndex = qualifier.IndexOf('-');
                if (separatorIndex <= 0 || separatorIndex == qualifier.Length - 1)
                {
                    return false;
                }

                var name = NormalizeQualifierName(qualifier[..separatorIndex]);
                var value = qualifier[(separatorIndex + 1)..];
                qualifiers.TryAdd(name, value);
            }
        }

        candidate = new IconCandidate(candidatePath, qualifiers);
        return true;
    }

    private static string NormalizeQualifierName(string name)
    {
        var normalizedName = name.ToLowerInvariant();
        return normalizedName switch
        {
            "alternateform" => "altform",
            "config" => "configuration",
            "lang" => "language",
            "layoutdir" => "layoutdirection",
            _ => normalizedName,
        };
    }

    private static IconSearchResult GetResolvedResourceFallback(string? resolvedResourcePath)
    {
        if (string.IsNullOrEmpty(resolvedResourcePath) || !File.Exists(resolvedResourcePath))
        {
            return IconSearchResult.NotFound();
        }

        var isHighContrast = resolvedResourcePath.Contains("contrast-", StringComparison.OrdinalIgnoreCase);
        return IconSearchResult.FoundScaled(resolvedResourcePath, isHighContrast);
    }

    private sealed class IconCandidate
    {
        internal string Path { get; }

        internal Dictionary<string, string> Qualifiers { get; }

        internal int? TargetSize { get; }

        internal int? Scale { get; }

        internal string? Contrast { get; }

        internal string? Theme { get; }

        internal string? AlternateForm { get; }

        internal bool IsHighContrast =>
            Contrast is not null && !string.Equals(Contrast, "standard", StringComparison.OrdinalIgnoreCase);

        internal IconCandidate(string path, Dictionary<string, string> qualifiers)
        {
            Path = path;
            Qualifiers = qualifiers;
            TargetSize = GetPositiveIntegerQualifier("targetsize", Qualifiers);
            Scale = GetPositiveIntegerQualifier("scale", Qualifiers);
            Qualifiers.TryGetValue("contrast", out var contrast);
            Contrast = contrast;
            Qualifiers.TryGetValue("theme", out var theme);
            Theme = theme;
            Qualifiers.TryGetValue("altform", out var alternateForm);
            AlternateForm = alternateForm;
        }

        private static int? GetPositiveIntegerQualifier(string name, Dictionary<string, string> qualifiers)
        {
            return qualifiers.TryGetValue(name, out var value) &&
                int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedValue) &&
                parsedValue > 0
                    ? parsedValue
                    : null;
        }
    }
}
