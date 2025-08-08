// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine.Invocation;
using PowerToys.DSC.Options;

namespace PowerToys.DSC.Commands;

internal sealed class ManifestCommand : BaseCommand
{
    private readonly OutputDirectoryOption _outputDirectoryOption;

    public ManifestCommand()
        : base("manifest", "Get the manifest of the dsc resource")
    {
        _outputDirectoryOption = new OutputDirectoryOption();
        AddOption(_outputDirectoryOption);
    }

    public override void CommandHandlerInternal(InvocationContext context)
    {
        var outputDir = context.ParseResult.GetValueForOption(_outputDirectoryOption);

        context.ExitCode = Resource!.Manifest(outputDir) ? 0 : 1;
    }
}
