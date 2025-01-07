// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Registry;

public partial class RegistryCommandsProvider : CommandProvider
{
    public RegistryCommandsProvider()
    {
        Id = "Windows.Registry";
        DisplayName = $"Windows Registry";
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return [
            new CommandItem(new RegistryListPage())
            {
                Title = "Registry",
                Subtitle = "Navigate the Windows registry",
            }
        ];
    }
}
