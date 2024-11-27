// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ActionBarContextItemViewModel : ObservableObject
{
    private readonly ICommandItem _commandItem;

    [ObservableProperty]
    public partial string Title { get; set; }

    ////private IconDataType Icon => Command.Icon;

    // TODO: do we want the icon here or get it over in the UI project?
    ////[ObservableProperty]
    ////private IconElement IcoElement => Microsoft.Terminal.UI.IconPathConverter.IconMUX(Icon.Icon);

    public ICommand? Command => _commandItem.Command;

    public ActionBarContextItemViewModel(ICommandContextItem contextItem)
    {
        _commandItem = contextItem;
        Title = _commandItem.Title;
    }
}
