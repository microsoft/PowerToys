// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class ShellItemIconFallback
{
    private static readonly Uri FallbackUri = new("ms-appx:///Assets/Icons/ShellItemIconFallback.svg");

    private static IconSource? _source;

    public static IconSource GetOrCreate()
    {
        // IconLoaderService calls this only from its WinUI dispatcher. Like the app-icon
        // fallback, one process-local source can be shared by every presentation size.
        var source = Volatile.Read(ref _source);
        if (source is null)
        {
            source = new ImageIconSource
            {
                ImageSource = new SvgImageSource(FallbackUri),
            };
            Volatile.Write(ref _source, source);
        }

        return source;
    }

    public static bool IsFallback(IconSource? source) =>
        ReferenceEquals(Volatile.Read(ref _source), source);
}
