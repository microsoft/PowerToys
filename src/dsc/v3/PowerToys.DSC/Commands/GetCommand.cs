// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine.Invocation;

namespace PowerToys.DSC.Commands;

/// <summary>
/// Command to get the resource state.
/// </summary>
internal sealed class GetCommand : BaseCommand
{
    public GetCommand()
        : base("get", "Get the resource state")
    {
    }

    /// <inheritdoc/>
    public override void CommandHandlerInternal(InvocationContext context)
    {
        context.ExitCode = Resource!.Get(Input) ? 0 : 1;
    }
}
