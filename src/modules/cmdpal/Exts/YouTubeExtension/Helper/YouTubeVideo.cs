// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeExtension.Helper;

public sealed class YouTubeVideo
{
    public string Title { get; init; } = string.Empty;

    public string Link { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;
}
