// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public class CommandContextItem : ICommandContextItem
{
    public bool IsCritical { get; set; }

    public ICommand Command { get; set; }

    public string Tooltip { get; set; } = string.Empty;

    public CommandContextItem(ICommand command)
    {
        Command = command;
    }
}
