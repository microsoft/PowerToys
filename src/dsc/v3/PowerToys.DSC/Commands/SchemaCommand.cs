// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine.Invocation;

namespace PowerToys.DSC.Commands;

/// <summary>
/// Command to output the schema of the resource.
/// </summary>
internal sealed class SchemaCommand : BaseCommand
{
    public SchemaCommand()
        : base("schema", "Outputs schema of the resource")
    {
    }

    /// <inheritdoc/>
    public override void CommandHandlerInternal(InvocationContext context)
    {
        context.ExitCode = Resource!.Schema() ? 0 : 1;
    }
}
