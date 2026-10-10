// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class SvgIconProtocolProcessor : IIconProtocolProcessor
{
    public static SvgIconProtocolProcessor Instance { get; } = new();

    public IconCachePartition CachePartition => IconCachePartition.Other;

    public ReadOnlySpan<string> ProtocolPrefixes => SvgIconProtocol.ProtocolPrefixes;

    private SvgIconProtocolProcessor()
    {
    }

    public string GetCacheIdentity(string value)
    {
        return SvgIconProtocol.GetCacheIdentity(value);
    }

    public IconRenderContext GetCacheContext(string value, IconRenderContext context)
    {
        return SvgIconProtocol.GetCacheContext(value, context);
    }

    public IconLoadInputKind ClassifyInput(string value)
    {
        return SvgIconProtocol.Classify(value) switch
        {
            SvgIconProtocol.Kind.PlainFile => IconLoadInputKind.SvgFile,
            SvgIconProtocol.Kind.PlainInline => IconLoadInputKind.SvgInline,
            SvgIconProtocol.Kind.ThemedFile => IconLoadInputKind.ThemedSvgFile,
            SvgIconProtocol.Kind.ThemedInline => IconLoadInputKind.ThemedSvgInline,
            _ => IconLoadInputKind.String,
        };
    }

    public bool TryPrepareSynchronously(
        string value,
        int targetSize,
        IconRenderContext context,
        out IconPathConverter.PreparedIcon preparedIcon)
    {
        preparedIcon = SvgIconProtocol.TryCreateSvg(value, context, out var svg)
            ? IconPathConverter.PreparedIcon.FromSvgData(svg, targetSize)
            : IconPathConverter.PreparedIcon.Empty();
        return true;
    }

    public ValueTask<IconProtocolProcessingResult> PrepareAsync(
        string value,
        int targetSize,
        IconRenderContext context)
    {
        _ = TryPrepareSynchronously(value, targetSize, context, out var preparedIcon);
        return ValueTask.FromResult(IconProtocolProcessingResult.FromPreparedIcon(preparedIcon));
    }
}
