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
    public IReadOnlyList<IContextItemViewModel> MoreCommands { get; }

    public bool HasMoreCommands { get; }

    public bool CanOpenContextMenu { get; }

    public IReadOnlyList<IContextItemViewModel> AllCommands { get; }

    public Dictionary<KeyChord, CommandContextItemViewModel> Keybindings()
    {
        var result = new Dictionary<KeyChord, CommandContextItemViewModel>();

        var menu = MoreCommands;
        if (menu is null)
        {
            return result;
        }

        foreach (var item in menu)
        {
            if (item is CommandContextItemViewModel cmd && cmd.HasRequestedShortcut)
            {
                var key = cmd.RequestedShortcut ?? new KeyChord(0, 0, 0);
                var added = result.TryAdd(key, cmd);
                if (!added)
                {
                    CoreLogger.LogWarning($"Ignoring duplicate keyboard shortcut {KeyChordHelpers.FormatForDebug(key)} on command '{cmd.Title ?? cmd.Name ?? "(unknown)"}'");
                }
            }
        }

        return result;
    }
}

public interface ICommandBarContext : IContextMenuContext
{
    public string SecondaryCommandName { get; }

    public CommandItemViewModel? PrimaryCommand { get; }

    public CommandItemViewModel? SecondaryCommand { get; }
}