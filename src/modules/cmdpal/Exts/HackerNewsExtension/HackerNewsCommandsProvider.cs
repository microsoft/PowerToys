// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace HackerNewsExtension;

public partial class HackerNewsCommandsProvider : CommandProvider
{
    public HackerNewsCommandsProvider()
    {
        DisplayName = "Hacker News Commands";
    }

    private readonly ICommandItem[] _actions = [
        new CommandItem(new HackerNewsPage()),
    ];

    public override ICommandItem[] TopLevelCommands()
    {
        return _actions;
    }
}
