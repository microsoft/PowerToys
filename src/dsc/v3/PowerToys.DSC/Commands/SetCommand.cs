// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine.Invocation;
using PowerToys.DSC.Properties;

namespace PowerToys.DSC.Commands;

/// <summary>
/// Command to set the resource state.
/// </summary>
public sealed class SetCommand : BaseCommand
{
    public SetCommand()
        : base("set", Resources.SetCommandDescription)
    {
    }

    /// <inheritdoc/>
    public override void CommandHandlerInternal(InvocationContext context)
    {
        context.ExitCode = Resource!.SetState(Input) ? 0 : 1;
    }
}
