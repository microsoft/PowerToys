// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class AppIconProtocolProcessor : IIconProtocolProcessor
{
    private readonly Func<string, bool, Task<IRandomAccessStream?>> _getThumbnail;

    public static AppIconProtocolProcessor Instance { get; } = new();

    private AppIconProtocolProcessor()
        : this(ThumbnailHelper.GetThumbnail)
    {
    }

    internal AppIconProtocolProcessor(Func<string, bool, Task<IRandomAccessStream?>> getThumbnail)
    {
        _getThumbnail = getThumbnail;
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
        _ = targetSize;
        _ = theme;

        if (!AppIconProtocol.TryParse(value, out var candidates, out var jumbo))
        {
            return IconProtocolProcessingResult.Empty();
        }

        foreach (var candidate in candidates)
        {
            try
            {
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

        return IconProtocolProcessingResult.FromFallbackIconString(candidates[0]);
    }
}
