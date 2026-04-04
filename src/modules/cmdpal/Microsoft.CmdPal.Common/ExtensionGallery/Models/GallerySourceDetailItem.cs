// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.ExtensionGallery.Models;

public sealed class GallerySourceDetailItem
{
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public Uri? LinkUri { get; set; }

    public bool HasLink => LinkUri is not null;

    public bool HasNoLink => !HasLink;
}
