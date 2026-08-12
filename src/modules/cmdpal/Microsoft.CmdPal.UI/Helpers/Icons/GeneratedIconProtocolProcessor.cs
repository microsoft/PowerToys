// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class GeneratedIconProtocolProcessor : IIconProtocolProcessor
{
    public static GeneratedIconProtocolProcessor Instance { get; } = new();

    private GeneratedIconProtocolProcessor()
    {
    }

    public IconCachePartition CachePartition => IconCachePartition.Other;

    public ReadOnlySpan<string> ProtocolPrefixes => GeneratedIconProtocol.ProtocolPrefixes;

    public string GetCacheIdentity(string value) => GeneratedIconProtocol.GetCacheIdentity(value);

    public ElementTheme GetCacheTheme(string value, ElementTheme theme) =>
        GeneratedIconProtocol.GetCacheTheme(value, theme);

    public IconLoadInputKind ClassifyInput(string value) =>
        GeneratedIconProtocol.Classify(value) switch
        {
            GeneratedIconProtocol.Kind.Swatch => IconLoadInputKind.GeneratedSwatch,
            GeneratedIconProtocol.Kind.Initials => IconLoadInputKind.GeneratedInitials,
            _ => IconLoadInputKind.String,
        };

    public bool TryPrepareSynchronously(
        string value,
        int targetSize,
        ElementTheme theme,
        out IconPathConverter.PreparedIcon preparedIcon)
    {
        if (GeneratedIconProtocol.Classify(value) == GeneratedIconProtocol.Kind.Initials)
        {
            // Font fallback and outline extraction must never run in a synchronous
            // caller such as the WinUI STA. PrepareAsync owns all initials work.
            preparedIcon = null!;
            return false;
        }

        preparedIcon = GeneratedIconProtocol.TryCreateSwatchSvg(value, theme, out var svg)
            ? IconPathConverter.PreparedIcon.FromSvgData(svg, targetSize)
            : IconPathConverter.PreparedIcon.Empty();
        return true;
    }

    public async ValueTask<IconProtocolProcessingResult> PrepareAsync(
        string value,
        int targetSize,
        ElementTheme theme)
    {
        if (GeneratedIconProtocol.Classify(value) != GeneratedIconProtocol.Kind.Initials)
        {
            _ = TryPrepareSynchronously(value, targetSize, theme, out var synchronousIcon);
            return IconProtocolProcessingResult.FromPreparedIcon(synchronousIcon);
        }

        // The loader currently calls async processors from a worker, but keep this
        // boundary independently safe if another caller reaches it from the STA.
        return await Task.Run(
            () =>
            {
                var preparedIcon = GeneratedIconProtocol.TryCreateInitialsSvg(value, theme, out var svg)
                    ? IconPathConverter.PreparedIcon.FromSvgData(svg, targetSize)
                    : IconPathConverter.PreparedIcon.Empty();
                return IconProtocolProcessingResult.FromPreparedIcon(preparedIcon);
            }).ConfigureAwait(false);
    }
}
