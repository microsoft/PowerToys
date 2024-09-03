// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;

namespace SpongebotExtension;

internal sealed class SpongebotCommandsProvider : ICommandProvider
{
    public string DisplayName => $"Spongebob, mocking";

    public IconDataType Icon => new(string.Empty);

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    public IListItem[] TopLevelCommands()
    {
        var spongebotPage = new SpongebotPage();
        var listItem = new ListItem(spongebotPage)
        {
            MoreCommands = [new CommandContextItem(spongebotPage.CopyTextAction)],
        };
        return [listItem];
    }
}
