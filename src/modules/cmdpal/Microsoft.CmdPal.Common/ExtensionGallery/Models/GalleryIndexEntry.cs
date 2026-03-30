// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.ExtensionGallery.Models;

public sealed class GalleryIndexEntry
{
    public string Id { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];
}
