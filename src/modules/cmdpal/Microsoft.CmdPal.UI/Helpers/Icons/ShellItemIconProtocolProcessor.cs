// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class ShellItemIconProtocolProcessor : IIconProtocolProcessor
{
    public static ShellItemIconProtocolProcessor Instance { get; } = new();

    private ShellItemIconProtocolProcessor()
    {
    }

    public IconCachePartition CachePartition => IconCachePartition.Other;

    public ReadOnlySpan<string> ProtocolPrefixes => ShellItemIconProtocol.ProtocolPrefixes;

    public string GetCacheIdentity(string value) => value;

    public ElementTheme GetCacheTheme(string value, ElementTheme theme) => ElementTheme.Default;

    public IconLoadInputKind ClassifyInput(string value) => IconLoadInputKind.ShellItemIcon;

    public bool TryPrepareSynchronously(
        string value,
        int targetSize,
        ElementTheme theme,
        out IconPathConverter.PreparedIcon preparedIcon)
    {
        preparedIcon = null!;
        return false;
    }

    public ValueTask<IconProtocolProcessingResult> PrepareAsync(
        string value,
        int targetSize,
        ElementTheme theme)
    {
        _ = value;
        _ = targetSize;
        _ = theme;
        return ValueTask.FromResult(IconProtocolProcessingResult.Empty());
    }
}
