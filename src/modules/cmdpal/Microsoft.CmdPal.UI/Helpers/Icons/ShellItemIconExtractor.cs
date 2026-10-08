// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class ShellItemIconExtractor : IShellItemIconExtractor
{
    public static ShellItemIconExtractor Instance { get; } = new();

    private ShellItemIconExtractor()
    {
    }

    public ValueTask<ShellIconExtractionResult> ExtractAsync(
        LocatedShellIcon locatedIcon,
        int targetPixelSize)
    {
        if (locatedIcon.Identity.Kind == ShellIconIdentityKind.SystemImageList)
        {
            return ValueTask.FromResult(
                ShellSystemImageListIconExtractor.Extract(
                    locatedIcon.Identity.SystemImageListIndex,
                    locatedIcon.Identity.Jumbo,
                    targetPixelSize));
        }

        return ExtractStreamAsync(locatedIcon.Request);
    }

    private static async ValueTask<ShellIconExtractionResult> ExtractStreamAsync(
        ShellItemIconRequest request)
    {
        var stream = await ThumbnailHelper
            .GetThumbnail(request.ItemPath, request.Jumbo)
            .ConfigureAwait(false);
        return stream is null
            ? ShellIconExtractionResult.Empty()
            : ShellIconExtractionResult.FromBitmapStream(stream);
    }
}
