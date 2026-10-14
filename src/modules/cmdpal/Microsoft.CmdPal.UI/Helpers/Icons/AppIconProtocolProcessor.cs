// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class AppIconProtocolProcessor : IIconProtocolProcessor
{
    private readonly Func<string, bool, Task<IRandomAccessStream?>> _getThumbnail;
    private readonly Func<string, int, SoftwareBitmap?>? _getJumboIcon;
    private readonly Func<string, int, SoftwareBitmap?>? _getShortcutIcon;

    public static AppIconProtocolProcessor Instance { get; } = new();

    private AppIconProtocolProcessor()
        : this(ThumbnailHelper.GetThumbnail, ShellItemImageFactoryIconExtractor.Extract, ExtractShortcutIcon)
    {
    }

    internal AppIconProtocolProcessor(
        Func<string, bool, Task<IRandomAccessStream?>> getThumbnail,
        Func<string, int, SoftwareBitmap?>? getJumboIcon = null,
        Func<string, int, SoftwareBitmap?>? getShortcutIcon = null)
    {
        _getThumbnail = getThumbnail;
        _getJumboIcon = getJumboIcon;
        _getShortcutIcon = getShortcutIcon;
    }

    public IconCachePartition CachePartition => IconCachePartition.Other;

    public ReadOnlySpan<string> ProtocolPrefixes => AppIconProtocol.ProtocolPrefixes;

    public string GetCacheIdentity(string value) => value;

    public ElementTheme GetCacheTheme(string value, ElementTheme theme) => ElementTheme.Default;

    public IconLoadInputKind ClassifyInput(string value) => IconLoadInputKind.SpecializedAppIcon;

    public bool TryPrepareSynchronously(
        string value,
        int targetSize,
        ElementTheme theme,
        out IconPathConverter.PreparedIcon preparedIcon)
    {
        preparedIcon = null!;
        return false;
    }

    public async ValueTask<IconProtocolProcessingResult> PrepareAsync(
        string value,
        int targetSize,
        ElementTheme theme)
    {
        _ = theme;

        if (!AppIconProtocol.TryParse(value, out var candidates, out var jumbo))
        {
            return IconProtocolProcessingResult.Empty();
        }

        for (var candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            var candidate = candidates[candidateIndex];
            if (candidate.Contains('/') && Path.IsPathFullyQualified(candidate))
            {
                // Native icon loaders need Windows separators, including for cached file paths.
                candidate = candidate.Replace('/', Path.DirectorySeparatorChar);
                candidates[candidateIndex] = candidate;
            }

            try
            {
                if (jumbo && _getJumboIcon?.Invoke(candidate, targetSize) is { } bitmap)
                {
                    return IconProtocolProcessingResult.FromPreparedIcon(IconPathConverter.PreparedIcon.FromBinary(bitmap));
                }

                if (!jumbo && candidate.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (_getShortcutIcon?.Invoke(candidate, targetSize) is { } shortcutBitmap)
                        {
                            return IconProtocolProcessingResult.FromPreparedIcon(IconPathConverter.PreparedIcon.FromBinary(shortcutBitmap));
                        }
                    }
                    catch
                    {
                        // Keep the ordinary thumbnail fallback if base-icon extraction fails.
                    }
                }

                if (await _getThumbnail(candidate, jumbo).ConfigureAwait(false) is { } stream)
                {
                    return IconProtocolProcessingResult.FromBitmapStream(stream);
                }
            }
            catch
            {
                // Continue with the next candidate before using the ordinary converter.
            }
        }

        return IconProtocolProcessingResult.FromFallbackIconStrings(candidates);
    }

    private static SoftwareBitmap? ExtractShortcutIcon(string path, int targetSize)
    {
        var request = new ShellItemIconRequest(path, jumbo: false);

        // A missing item's registered type icon must not mask later app candidates.
        if (!ShellItemIconLocator.Instance.TryLocate(request, out var locatedIcon)
            || !locatedIcon.CacheRawRequestAlias
            || locatedIcon.Identity.Kind != ShellIconIdentityKind.SystemImageList)
        {
            return null;
        }

        // App-icon requests use the base entry to omit shortcut overlays.
        // Keep the shared Toolkit thumbnail API's decorated shortcut behavior.
        using var extraction = ShellSystemImageListIconExtractor.Extract(
            locatedIcon.Identity.SystemImageListIndex,
            jumbo: false,
            requestedPixelSize: targetSize);
        return extraction.TakeSoftwareBitmap();
    }
}
