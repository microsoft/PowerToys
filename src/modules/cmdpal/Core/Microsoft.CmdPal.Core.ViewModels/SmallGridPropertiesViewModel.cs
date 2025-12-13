// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public class SmallGridPropertiesViewModel : IGridPropertiesViewModel
{
    private readonly ExtensionObject<ISmallGridLayout> _model;

    public bool ShowTitle => false;

    public bool ShowSubtitle => false;

    public SmallGridPropertiesViewModel(ISmallGridLayout smallGridLayout)
    {
        _model = new(smallGridLayout);
    }

    public void InitializeProperties()
    {
    }
}
