// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public class SmallListPropertiesViewModel : IGridPropertiesViewModel
{
    public static readonly SmallListPropertiesViewModel Default = new(null!);

    private readonly ExtensionObject<ISmallListLayout> _model;

    public bool IsGrid => false;

    public bool ShowTitle => true;

    public bool ShowSubtitle => true;

    public ContentSize AutomaticWrappingBreakpoint { get; private set; } = ContentSize.Medium;

    public bool IsAutomaticWrappingEnabled { get; private set; } = true;

    public SmallListPropertiesViewModel(ISmallListLayout layout)
    {
        _model = new(layout);
    }

    public void InitializeProperties()
    {
        if (_model.Unsafe is { } model)
        {
            IsAutomaticWrappingEnabled = model.IsAutomaticWrappingEnabled;
            AutomaticWrappingBreakpoint = model.AutomaticWrappingBreakpoint;
        }
    }
}
