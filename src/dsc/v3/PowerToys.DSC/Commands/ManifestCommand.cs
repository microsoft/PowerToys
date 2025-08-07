// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine.Invocation;

namespace PowerToys.DSC.Commands;

internal sealed class ManifestCommand : BaseCommand
{
    public ManifestCommand()
        : base("manifest", "Get the manifest of the dsc resource")
    {
    }

    public override void CommandHandlerInternal(InvocationContext context)
    {
    }
}
