// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.ExtensionGallery.Models;

public sealed class GallerySourceInfo
{
    public string Kind { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Id { get; set; }

    public string? Uri { get; set; }

    public bool IsKnown { get; set; }

    public GallerySourceDetails? Details { get; set; }

    public bool HasDetails => Details?.HasContent == true;
}
