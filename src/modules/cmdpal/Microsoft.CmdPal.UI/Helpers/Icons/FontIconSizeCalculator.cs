// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class FontIconSizeCalculator
{
    private const int MinimumFallbackSize = 8;

    public static int Calculate(Size iconSize, double scale, int defaultSize)
    {
        var scaledSize = iconSize.IsEmpty
            ? iconSize
            : new Size(iconSize.Width * scale, iconSize.Height * scale);
        var targetSize = scaledSize.IsEmpty
            ? defaultSize
            : (int)Math.Max(scaledSize.Width, scaledSize.Height);
        return targetSize > 0 ? targetSize : MinimumFallbackSize;
    }
}
