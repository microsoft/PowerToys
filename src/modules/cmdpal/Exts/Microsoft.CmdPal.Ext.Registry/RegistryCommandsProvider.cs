// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Registry.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Registry;

public partial class RegistryCommandsProvider : CommandProvider
{
    public RegistryCommandsProvider()
    {
        Id = "Windows.Registry";
        DisplayName = Resources.RegistryProvider_DisplayName;
        Icon = IconHelpers.FromRelativePath("Assets\\Registry.svg");
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
