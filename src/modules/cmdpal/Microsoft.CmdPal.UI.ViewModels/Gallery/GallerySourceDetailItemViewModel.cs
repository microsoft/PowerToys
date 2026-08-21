// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Gallery;

public sealed class GallerySourceDetailItemViewModel
{
    public string Label { get; }

    public string Value { get; }

    public Uri? LinkUri { get; }

    public bool HasLink => LinkUri is not null;

    public bool HasNoLink => !HasLink;

    public GallerySourceDetailItemViewModel(string label, string value, Uri? linkUri)
    {
        Label = label;
        Value = value;
        LinkUri = linkUri;
    }
}
