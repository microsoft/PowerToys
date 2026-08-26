// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.CmdPal.Common;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Messages;

public interface IContextMenuContext : INotifyPropertyChanged
{
    IReadOnlyList<IContextItemViewModel> MoreCommands { get; }

    bool HasMoreCommands { get; }

    bool CanOpenContextMenu { get; }

    IReadOnlyList<IContextItemViewModel> AllCommands { get; }

    Dictionary<KeyChord, CommandContextItemViewModel> Keybindings()
    {
        var result = new Dictionary<KeyChord, CommandContextItemViewModel>();
        foreach (var item in MoreCommands)
        {
            if (item is CommandContextItemViewModel command && command.HasRequestedShortcut)
            {
                var key = command.RequestedShortcut ?? new KeyChord(0, 0, 0);
                if (!result.TryAdd(key, command))
                {
                    CoreLogger.LogWarning($"Ignoring duplicate keyboard shortcut {KeyChordHelpers.FormatForDebug(key)} on command '{command.Title ?? command.Name ?? "(unknown)"}'");
                }
            }
        }

        return result;
    }
}

public interface ICommandBarContext : IContextMenuContext
{
    string SecondaryCommandName { get; }

    CommandItemViewModel? PrimaryCommand { get; }

    CommandItemViewModel? SecondaryCommand { get; }
}
