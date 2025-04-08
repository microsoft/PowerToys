// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContextMenuStackViewModel : ObservableObject
{
    [ObservableProperty]
    public partial ObservableCollection<CommandContextItemViewModel> ContextCommands { get; set; }

    // private Dictionary<KeyChord, CommandContextItemViewModel>? _contextKeybindings;
    public ContextMenuStackViewModel(IEnumerable<CommandContextItemViewModel> commands)
    {
        ContextCommands = [.. commands];
    }
}
