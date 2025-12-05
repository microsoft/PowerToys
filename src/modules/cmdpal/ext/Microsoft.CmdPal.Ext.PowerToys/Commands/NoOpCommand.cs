// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Commands;

internal sealed partial class NoOpCommand : InvokableCommand
{
    internal NoOpCommand(string title = "No operation")
    {
        Name = title;
    }

    public override CommandResult Invoke()
    {
        return CommandResult.Hide();
    }
}
