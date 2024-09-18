// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ActionBarViewModel : ObservableObject
{
    [ObservableProperty]
    private string _actionName = string.Empty;

    [ObservableProperty]
    private bool _moreCommandsAvailable = false;

    [ObservableProperty]
    private ObservableCollection<ActionBarContextItemViewModel> _contextActions = [];

    public ActionBarViewModel()
    {
        // Just for fun
        ActionName = "My Action";
        MoreCommandsAvailable = true;
        ContextActions.Add(new ActionBarContextItemViewModel("Action1", true));
        ContextActions.Add(new ActionBarContextItemViewModel("Action2", true));
    }
}
