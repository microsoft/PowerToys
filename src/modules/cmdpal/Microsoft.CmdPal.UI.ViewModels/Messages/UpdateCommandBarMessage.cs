// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

/// <summary>
/// Used to update the command bar at the bottom to reflect the commands for a list item
/// </summary>
public record UpdateCommandBarMessage(ICommandBarContext? ViewModel)
{
}

// Represents everything the command bar needs to know about to show command
// buttons at the bottom.
//
// This is implemented by both ListItemViewModel and ContentPageViewModel,
// the two things with sub-commands.
public interface ICommandBarContext : INotifyPropertyChanged
{
    public IEnumerable<CommandContextItemViewModel> MoreCommands { get; }

    public bool HasMoreCommands { get; }

    public string SecondaryCommandName { get; }

    public CommandItemViewModel? PrimaryCommand { get; }

    public CommandItemViewModel? SecondaryCommand { get; }

    public List<CommandContextItemViewModel> AllCommands { get; }
}
