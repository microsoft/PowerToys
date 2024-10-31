// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.WindowsServices;

public partial class WindowsServicesCommandsProvider : ICommandProvider
{
    public string DisplayName => $"Windows Services";

    public WindowsServicesCommandsProvider()
    {
    }

    public IconDataType Icon => new(string.Empty);

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    public IListItem[] TopLevelCommands()
    {
        return [
            new ListItem(new ServicesListPage())
            {
                Title = "Search Windows Services",
                Subtitle = "Quickly manage all Windows Services",
            }
        ];
    }
}
