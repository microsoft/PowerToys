// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using SSHKeychainExtension;

namespace SamplePagesExtension;

public class SamplePagesCommandsProvider : ICommandProvider
{
    public string DisplayName => $"Sample Pages Commands";

    public IconDataType Icon => new(string.Empty);

    private readonly IListItem[] _commands = [
       new ListItem(new SampleMarkdownPage())
       {
           Title = "Markdown Page Sample Command",
           Subtitle = "SamplePages Extension",
       },
       new ListItem(new SampleListPage())
       {
           Title = "List Page Sample Command",
           Subtitle = "SamplePages Extension",
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
