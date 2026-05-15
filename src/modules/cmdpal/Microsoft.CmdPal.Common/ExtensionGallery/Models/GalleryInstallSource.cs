// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.ExtensionGallery.Models;

public sealed class GalleryInstallSource
{
    public string Type { get; set; } = string.Empty;

    public string? Id { get; set; }

    public string? Uri { get; set; }
}
