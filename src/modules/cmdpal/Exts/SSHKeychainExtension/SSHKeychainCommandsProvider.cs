// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace SSHKeychainExtension;

public partial class SSHKeychainCommandsProvider : CommandProvider
{
    public SSHKeychainCommandsProvider()
    {
        DisplayName = "SSH Keychain Commands";
    }

    private readonly ICommandItem[] _commands = [
       new CommandItem(new SSHHostsListPage())
        {
            Title = "Search SSH Keys",
            Subtitle = "Quickly find and launch into hosts from your SSH config file",
        },
    ];

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }
}
