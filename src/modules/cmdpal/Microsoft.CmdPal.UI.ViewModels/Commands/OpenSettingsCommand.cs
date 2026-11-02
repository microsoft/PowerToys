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
    public const string SettingsCommandId = "com.microsoft.cmdpal.opensettings"; /* #no-spell-check-line */

    public const string ExtensionSettingsCommandId = "com.microsoft.cmdpal.openextensionsettings"; /* #no-spell-check-line */

    private const string SettingsGlyph = "\uE713";

    public OpenSettingsCommand()
        : this(
            settingsPageTag: string.Empty,
            name: Properties.Resources.builtin_open_settings_name,
            glyph: SettingsGlyph,
            id: SettingsCommandId)
    {
    }

    public OpenSettingsCommand(OpenSettingsMessage destination, string name, string id)
        : this(string.Empty, name, SettingsGlyph, id)
    {
        ArgumentNullException.ThrowIfNull(destination);
        _destination = destination;
    }

    protected OpenSettingsCommand(
        string settingsPageTag,
        string name,
        string glyph,
        string id)
    {
        _destination = new OpenSettingsMessage(settingsPageTag);
        Name = name;
        Icon = new IconInfo(glyph);
        Id = id;
    }

    private readonly OpenSettingsMessage _destination;

    public override ICommandResult Invoke()
    {
        WeakReferenceMessenger.Default.Send(_destination);
        return CommandResult.KeepOpen();
    }
}
