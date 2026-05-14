// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.ExtensionGallery.Models;

/// <summary>
/// Represents the wrapped gallery index format where extension data is inline.
/// </summary>
public sealed class GalleryRemoteIndex
{
    public List<GalleryExtensionEntry> Extensions { get; set; } = [];
}
