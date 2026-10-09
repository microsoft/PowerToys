// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.ExtensionGallery.Models;

public sealed class GalleryExtensionEntry
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? ShortDescription { get; set; }

    public GalleryAuthor Author { get; set; } = new();

    public string? Homepage { get; set; }

    public string? Readme { get; set; }

    public string? IconUrl { get; set; }

    public List<string> ScreenshotUrls { get; set; } = [];

    public List<GalleryInstallSource> InstallSources { get; set; } = [];

    public GalleryDetection? Detection { get; set; }

    public List<string> Tags { get; set; } = [];
}
