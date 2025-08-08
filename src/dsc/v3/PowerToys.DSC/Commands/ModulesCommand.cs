// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine.Invocation;

namespace PowerToys.DSC.Commands;

internal sealed class ModulesCommand : BaseCommand
{
    public ModulesCommand()
        : base("modules", "Get all supported modules")
    {
    }

    public override void CommandHandlerInternal(InvocationContext context)
    {
        // Print the supported modules for the specified resource
        foreach (var module in Resource!.GetSupportedModules())
        {
            Console.WriteLine(module);
        }
    }
}
