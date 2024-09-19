// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ActionBarContextItemViewModel : ObservableObject
{
    ////private ICommand _command;

    [ObservableProperty]
    private string _name = "Placeholder"; // Command.Name;

    ////private IconDataType Icon => Command.Icon;

    [ObservableProperty]
    private bool _canInvoke = true; // Command != null && Command is IInvokableCommand or IPage;

    // TODO: do we want the icon here or get it over in the UI project?
    ////[ObservableProperty]
    ////private IconElement IcoElement => Microsoft.Terminal.UI.IconPathConverter.IconMUX(Icon.Icon);

    public ActionBarContextItemViewModel(string name, bool canInvoke)
    {
        Name = name;
        CanInvoke = canInvoke;
    }
}
