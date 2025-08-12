// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;

namespace PowerToys.DSC.Commands;

/// <summary>
/// Command to get all supported modules for a specific resource.
/// </summary>
/// <remarks>
/// This class is primarly used for debugging purposes and for build scripts.
/// </remarks>
internal sealed class ModulesCommand : BaseCommand
{
    public ModulesCommand()
        : base("modules", "Get all supported modules for a specific resource")
    {
    }

    /// <inheritdoc/>
    public override void CommandHandlerInternal(InvocationContext context)
    {
        // Module is optional, if not provided, all supported modules for the
        // resource will be printed. If provided, it must be one of the
        // supported modules since it has been validated before this command is
        // executed.
        if (!string.IsNullOrEmpty(Module))
        {
            Debug.Assert(Resource!.GetSupportedModules().Contains(Module), "Module must be present in the list of supported modules.");
            context.Console.WriteLine(Module);
        }
        else
        {
            // Print the supported modules for the specified resource
            foreach (var module in Resource!.GetSupportedModules())
            {
                Console.WriteLine(module);
            }
        }
    }
}
