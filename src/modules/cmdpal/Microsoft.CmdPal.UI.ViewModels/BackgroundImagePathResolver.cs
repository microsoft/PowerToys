// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Resolves configured background image paths that can target either a single image file or a folder.
/// </summary>
public static class BackgroundImagePathResolver
{
    public static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".bmp",
        ".jpg",
        ".jpeg",
        ".jfif",
        ".gif",
        ".tiff",
        ".tif",
        ".webp",
        ".jxr",
    };

    /// <summary>
    /// Returns true when <paramref name="configuredPath"/> points to an existing local directory.
    /// </summary>
    public static bool TryGetLocalFolderPath(string? configuredPath, out string folderPath)
    {
        folderPath = string.Empty;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return false;
        }

        var candidate = configuredPath.Trim();

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var absoluteUri))
        {
            if (!absoluteUri.IsFile)
            {
                return false;
            }

            var localPath = absoluteUri.LocalPath;
            if (!Directory.Exists(localPath))
            {
                return false;
            }

            folderPath = localPath;
            return true;
        }

        if (!Uri.TryCreate(candidate, UriKind.RelativeOrAbsolute, out var uri) || uri.IsAbsoluteUri)
        {
            return false;
        }

        var localCandidate = Path.GetFullPath(candidate);
        if (!Directory.Exists(localCandidate))
        {
            return false;
        }

        folderPath = localCandidate;
        return true;
    }

    /// <summary>
    /// Gets sorted image files from <paramref name="folderPath"/> supported by the background pipeline.
    /// </summary>
    public static IReadOnlyList<string> GetSupportedImageFiles(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return [];
        }

        try
        {
            return Directory
                .EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                .Where(IsSupportedImageFile)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Resolves a stable preview path for settings UI. For folders, this returns the first supported image.
    /// </summary>
    public static string? ResolvePreviewImagePath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        if (!TryGetLocalFolderPath(configuredPath, out var folderPath))
        {
            return configuredPath.Trim();
        }

        var files = GetSupportedImageFiles(folderPath);
        return files.Count > 0 ? files[0] : null;
    }

    private static bool IsSupportedImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return SupportedImageExtensions.Contains(extension);
    }
}
