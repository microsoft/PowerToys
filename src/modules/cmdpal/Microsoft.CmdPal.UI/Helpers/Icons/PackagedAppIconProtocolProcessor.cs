// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Apps.Packaged;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class PackagedAppIconProtocolProcessor : IIconProtocolProcessor
{
    private static readonly string[] Prefixes = [PackagedAppIcons.ProtocolPrefix];

    public static PackagedAppIconProtocolProcessor Instance { get; } = new();

    public IconCachePartition CachePartition => IconCachePartition.Other;

    public ReadOnlySpan<string> ProtocolPrefixes => Prefixes;

    public string GetCacheIdentity(string value)
    {
        return value;
    }

    public IconRenderContext GetCacheContext(string value, IconRenderContext context)
    {
        // Packaged artwork uses the contrast qualifier, not the system foreground palette.
        return new(context.Theme, new(context.Contrast.Mode));
    }

    public IconLoadInputKind ClassifyInput(string value)
    {
        return IconLoadInputKind.SpecializedAppIcon;
    }

    public bool TryPrepareSynchronously(
        string value,
        int targetSize,
        IconRenderContext context,
        out IconPathConverter.PreparedIcon preparedIcon)
    {
        // PRI and directory reads belong on the icon loader's worker, never on the UI thread.
        preparedIcon = null!;
        return false;
    }

    public ValueTask<IconProtocolProcessingResult> PrepareAsync(
        string value,
        int targetSize,
        IconRenderContext context)
    {
        var packagedTheme = new PackagedIconTheme(context.Theme == ElementTheme.Light, context.Contrast.Mode);
        return new(PackagedAppIcons.TryResolve(value, packagedTheme, targetSize, out var path)
            ? IconProtocolProcessingResult.FromFallbackIconStrings([path])
            : IconProtocolProcessingResult.Empty());
    }
}
