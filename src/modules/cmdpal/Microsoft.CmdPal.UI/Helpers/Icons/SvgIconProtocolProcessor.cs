// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class SvgIconProtocolProcessor : IIconProtocolProcessor
{
    public static SvgIconProtocolProcessor Instance { get; } = new();

    private SvgIconProtocolProcessor()
    {
    }

    public IconCachePartition CachePartition => IconCachePartition.Other;

    public ReadOnlySpan<string> ProtocolPrefixes => SvgIconProtocol.ProtocolPrefixes;

    public string GetCacheIdentity(string value) => SvgIconProtocol.GetCacheIdentity(value);

    public ElementTheme GetCacheTheme(string value, ElementTheme theme) =>
        SvgIconProtocol.GetCacheTheme(value, theme);

    public IconLoadInputKind ClassifyInput(string value) =>
        SvgIconProtocol.Classify(value) switch
        {
            SvgIconProtocol.Kind.PlainFile => IconLoadInputKind.SvgFile,
            SvgIconProtocol.Kind.PlainInline => IconLoadInputKind.SvgInline,
            SvgIconProtocol.Kind.ThemedFile => IconLoadInputKind.ThemedSvgFile,
            SvgIconProtocol.Kind.ThemedInline => IconLoadInputKind.ThemedSvgInline,
            _ => IconLoadInputKind.String,
        };

    public bool TryPrepareSynchronously(
        string value,
        int targetSize,
        ElementTheme theme,
        out IconPathConverter.PreparedIcon preparedIcon)
    {
        preparedIcon = SvgIconProtocol.TryCreateSvg(value, theme, out var svg)
            ? IconPathConverter.PreparedIcon.FromSvgData(svg, targetSize)
            : IconPathConverter.PreparedIcon.Empty();
        return true;
    }

    public ValueTask<IconProtocolProcessingResult> PrepareAsync(
        string value,
        int targetSize,
        ElementTheme theme)
    {
        _ = TryPrepareSynchronously(value, targetSize, theme, out var preparedIcon);
        return ValueTask.FromResult(IconProtocolProcessingResult.FromPreparedIcon(preparedIcon));
    }
}
