// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace YouTubeExtension.Pages;

internal sealed partial class YouTubeAPIPage : FormPage
{
    private readonly YouTubeAPIForm apiForm = new();

    public override IForm[] Forms() => [apiForm];

    public YouTubeAPIPage()
    {
        Name = "Edit YouTube API Key";
        Icon = new IconInfo("https://www.youtube.com/favicon.ico");
    }
}
