// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

public partial class OpenSettingsCommand : InvokableCommand
{
    public OpenSettingsCommand()
        : this(
            settingsPageTag: string.Empty,
            name: Properties.Resources.builtin_open_settings_name,
            glyph: "\uE713",
            id: "com.microsoft.cmdpal.opensettings") /* #no-spell-check-line */
    {
    }

    protected OpenSettingsCommand(
        string settingsPageTag,
        string name,
        string glyph,
        string id)
    {
        _settingsPageTag = settingsPageTag;
        Name = name;
        Icon = new IconInfo(glyph);
        Id = id;
    }

    private readonly string _settingsPageTag;

    public override ICommandResult Invoke()
    {
        WeakReferenceMessenger.Default.Send(new OpenSettingsMessage(_settingsPageTag));
        return CommandResult.KeepOpen();
    }
}
