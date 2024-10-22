// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace SSHKeychainExtension;

public partial class SSHKeychainCommandsProvider : ICommandProvider
{
    public string DisplayName => $"SSH Keychain Commands";

    public IconDataType Icon => new(string.Empty);

    private readonly IListItem[] _commands = [
       new ListItem(new SSHHostsListPage())
        {
            Title = "Search SSH Keys",
            Subtitle = "Quickly find and launch into hosts from your SSH config file",
        },
    ];

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    public IListItem[] TopLevelCommands()
    {
        return _commands;
    }
}
