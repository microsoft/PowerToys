// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.Foundation;

namespace WindowsCommandPalette.BuiltinCommands;

public class ReloadExtensionsAction : InvokableCommand, IFallbackHandler
{
    public ReloadExtensionsAction()
    {
        Icon = new("\uE72C"); // Refresh icon
    }

    public override ICommandResult Invoke()
    {
        return ActionResult.GoHome();
    }

    public void UpdateQuery(string query)
    {
        if (query.StartsWith('r'))
        {
            Name = "Reload";
        }
        else
        {
            Name = string.Empty;
        }
    }
}
