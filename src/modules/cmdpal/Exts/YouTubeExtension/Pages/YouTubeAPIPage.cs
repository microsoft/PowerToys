// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace YouTubeExtension.Pages;

internal sealed partial class YouTubeAPIPage : ContentPage
{
    private readonly YouTubeAPIForm apiForm = new();

    public override IContent[] GetContent() => [apiForm];

    public YouTubeAPIPage()
    {
        Name = "Edit YouTube API Key";
        Icon = new IconInfo("https://www.youtube.com/favicon.ico");
    }
}
