// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.CmdPal.UI.ViewModels;

public class GalleryGridPropertiesViewModel : IGridPropertiesViewModel
{
    private readonly ExtensionObject<IGalleryGridLayout> _model;

    public bool ShowTitle { get; private set; }

    public bool ShowSubtitle { get; private set; }

    public Stretch ImageStretch { get; private set; } = Stretch.UniformToFill;

    public GalleryGridPropertiesViewModel(IGalleryGridLayout galleryGridLayout)
    {
        _model = new(galleryGridLayout);
    }

    public void InitializeProperties()
    {
        var model = _model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        ShowTitle = model.ShowTitle;
        ShowSubtitle = model.ShowSubtitle;
        ImageStretch = Stretch.UniformToFill;
        if (model is IExtendedAttributesProvider provider &&
            provider.GetProperties()?.TryGetValue(WellKnownExtensionAttributes.GalleryImageFit, out var imageFit) == true &&
            imageFit is int value && value == (int)GalleryImageFit.Contain)
        {
            ImageStretch = Stretch.Uniform;
        }
    }
}
