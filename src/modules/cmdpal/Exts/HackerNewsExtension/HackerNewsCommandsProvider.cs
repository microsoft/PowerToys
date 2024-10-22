// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace HackerNewsExtension;

public partial class HackerNewsCommandsProvider : ICommandProvider
{
    public string DisplayName => $"Hacker News Commands";

    public IconDataType Icon => new(string.Empty);

    private readonly IListItem[] _actions = [
        new ListItem(new HackerNewsPage()),
    ];

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    public IListItem[] TopLevelCommands()
    {
        return _actions;
    }
}
