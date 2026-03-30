// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.ExtensionGallery.Models;

public sealed class GalleryExtensionEntry
{
    public string Id { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(GalleryLocalizedStringConverter))]
    public string Title { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(GalleryLocalizedStringConverter))]
    public string Description { get; set; } = string.Empty;

    public GalleryAuthor Author { get; set; } = new();

    public string? Homepage { get; set; }

    [System.Text.Json.Serialization.JsonConverter(typeof(GalleryLocalizedStringConverter))]
    public string? Readme { get; set; }

    public string? Icon { get; set; }

    public string? IconDark { get; set; }

    public List<GalleryInstallSource> InstallSources { get; set; } = [];

    public GalleryDetection? Detection { get; set; }

    public List<string> Tags { get; set; } = [];
}
