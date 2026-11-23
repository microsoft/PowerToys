// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation.Collections;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class GalleryGridLayout : BaseObservable, IGalleryGridLayout, IExtendedAttributesProvider
{
    public virtual bool ShowTitle { get; set => SetProperty(ref field, value); } = true;

    public virtual bool ShowSubtitle { get; set => SetProperty(ref field, value); } = true;

    /// <summary>
    /// Gets or sets how images fit within their gallery tiles.
    /// </summary>
    public virtual GalleryImageFit ImageFit { get; set => SetProperty(ref field, value); } = GalleryImageFit.Cover;

    public virtual IDictionary<string, object>? GetProperties() => new ValueSet
    {
        { WellKnownExtensionAttributes.GalleryImageFit, (int)ImageFit },
    };
}
