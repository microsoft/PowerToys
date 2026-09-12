// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Helpers;

internal interface IIconProtocolProcessor
{
    IconCachePartition CachePartition { get; }

    ReadOnlySpan<string> ProtocolPrefixes { get; }

    ElementTheme GetCacheTheme(string value, ElementTheme theme);

    IconLoadInputKind ClassifyInput(string value);

    bool TryPrepareSynchronously(
        string value,
        int targetSize,
        ElementTheme theme,
        [MaybeNullWhen(false)] out IconPathConverter.PreparedIcon preparedIcon);

    ValueTask<IconProtocolProcessingResult> PrepareAsync(
        string value,
        int targetSize,
        ElementTheme theme);
}
