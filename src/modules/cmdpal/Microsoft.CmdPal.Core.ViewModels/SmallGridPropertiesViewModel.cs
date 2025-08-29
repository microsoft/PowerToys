// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.ViewModels;

public class SmallGridPropertiesViewModel : IGridPropertiesViewModel
{
    private readonly ExtensionObject<SmallGridLayout> _model;

    public SmallGridPropertiesViewModel(SmallGridLayout smallGridLayout)
    {
        _model = new(smallGridLayout);
    }

    public void InitializeProperties()
    {
    }
}
