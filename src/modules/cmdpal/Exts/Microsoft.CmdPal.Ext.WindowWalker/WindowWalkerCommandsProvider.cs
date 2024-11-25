// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CmdPal.Ext.WindowWalker.Components;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.WindowWalker;

public partial class WindowWalkerCommandsProvider : CommandProvider
{
    private readonly WalkerTopLevelCommandItem _walkerCommand;
    private readonly SettingsManager _settingsManager = new();

    internal static readonly VirtualDesktopHelper VirtualDesktopHelperInstance = new();

    public WindowWalkerCommandsProvider()
    {
        DisplayName = "Window Walker"; // TODO -- localization with properties please!

        _walkerCommand = new WalkerTopLevelCommandItem(_settingsManager);

        // _terminalCommand.MoreCommands = [new CommandContextItem(new SettingsPage(_settingsManager))];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return [_walkerCommand];
    }
}
