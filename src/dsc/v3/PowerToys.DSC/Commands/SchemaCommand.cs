// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using PowerToys.DSC.Properties;

namespace PowerToys.DSC.Commands;

/// <summary>
/// Command to output the schema of the resource.
/// </summary>
public sealed class SchemaCommand : BaseCommand
{
    public SchemaCommand()
        : base("schema", Resources.SchemaCommandDescription)
    {
    }

    /// <inheritdoc/>
    public override int CommandHandlerInternal(ParseResult parseResult)
    {
        return Resource!.Schema() ? 0 : 1;
    }
}
