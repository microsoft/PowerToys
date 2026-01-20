// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public class GalleryGridPropertiesViewModel : IGridPropertiesViewModel
{
    private readonly ExtensionObject<IGalleryGridLayout> _model;

    public bool ShowTitle { get; private set; }

    public bool ShowSubtitle { get; private set; }

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
    }
}
