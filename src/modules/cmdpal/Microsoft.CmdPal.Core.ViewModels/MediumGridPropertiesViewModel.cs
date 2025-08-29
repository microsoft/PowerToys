// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.ViewModels;

public class MediumGridPropertiesViewModel : IGridPropertiesViewModel
{
    private readonly ExtensionObject<MediumGridLayout> _model;

    public MediumGridPropertiesViewModel(MediumGridLayout mediumGridLayout)
    {
        _model = new(mediumGridLayout);
    }

    public bool ShowTitle { get; set; }

    public void InitializeProperties()
    {
        var model = _model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        ShowTitle = model.ShowTitle;
    }
}
