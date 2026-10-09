// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine.Invocation;
using PowerToys.DSC.Properties;

namespace PowerToys.DSC.Commands;

/// <summary>
/// Command to export all state instances.
/// </summary>
public sealed class ExportCommand : BaseCommand
{
    public ExportCommand()
        : base("export", Resources.ExportCommandDescription)
    {
    }

    /// <inheritdoc/>
    public override void CommandHandlerInternal(InvocationContext context)
    {
        context.ExitCode = Resource!.ExportState(Input) ? 0 : 1;
    }
}
