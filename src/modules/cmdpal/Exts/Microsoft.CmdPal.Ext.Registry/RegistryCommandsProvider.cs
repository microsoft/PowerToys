// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Registry;

public partial class RegistryCommandsProvider : ICommandProvider
{
    public string DisplayName => $"Windows Services";

    public RegistryCommandsProvider()
    {
    }

    public IconDataType Icon => new(string.Empty);

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    public IListItem[] TopLevelCommands()
    {
        return [
            new ListItem(new RegistryListPage())
            {
                Title = "Search the Windows Registry",
                Subtitle = "Navigates inside the Windows registry",
            }
        ];
    }
}
