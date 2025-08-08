// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine.Invocation;

namespace PowerToys.DSC.Commands;

internal sealed class ExportCommand : BaseCommand
{
    public ExportCommand()
        : base("export", "Get all state instances")
    {
    }

    public override void CommandHandlerInternal(InvocationContext context)
    {
        context.ExitCode = Resource!.Export(Input) ? 0 : 1;
    }
}
