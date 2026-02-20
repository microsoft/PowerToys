// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public class MediumListPropertiesViewModel : IGridPropertiesViewModel
{
    private readonly ExtensionObject<IMediumListLayout> _model;

    public bool IsGrid => false;

    public bool ShowTitle => true;

    public bool ShowSubtitle => true;

    public MediumListPropertiesViewModel(IMediumListLayout layout)
    {
        _model = new ExtensionObject<IMediumListLayout>(layout);
    }

    public void InitializeProperties()
    {
    }
}
