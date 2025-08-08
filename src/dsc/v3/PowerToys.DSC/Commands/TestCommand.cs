// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine.Invocation;

namespace PowerToys.DSC.Commands;

internal sealed class TestCommand : BaseCommand
{
    public TestCommand()
        : base("test", "Test the resource state")
    {
    }

    public override void CommandHandlerInternal(InvocationContext context)
    {
        context.ExitCode = Resource!.Test(Input) ? 0 : 1;
    }
}
