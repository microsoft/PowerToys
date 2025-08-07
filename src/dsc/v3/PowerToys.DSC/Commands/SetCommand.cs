// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine.Invocation;

namespace PowerToys.DSC.Commands;

internal sealed class SetCommand : BaseCommand
{
    public SetCommand()
        : base("set", "Set the resource state")
    {
    }

    public override void CommandHandlerInternal(InvocationContext context)
    {
    }
}
