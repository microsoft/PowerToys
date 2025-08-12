// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine.Invocation;
using PowerToys.DSC.Options;
using PowerToys.DSC.Properties;

namespace PowerToys.DSC.Commands;

/// <summary>
/// Command to get the manifest of the DSC resource.
/// </summary>
public sealed class ManifestCommand : BaseCommand
{
    /// <summary>
    /// Option to specify the output directory for the manifest.
    /// </summary>
    private readonly OutputDirectoryOption _outputDirectoryOption;

    public ManifestCommand()
        : base("manifest", Resources.ManifestCommandDescription)
    {
        _outputDirectoryOption = new OutputDirectoryOption();
        AddOption(_outputDirectoryOption);
    }

    /// <inheritdoc/>
    public override void CommandHandlerInternal(InvocationContext context)
    {
        var outputDir = context.ParseResult.GetValueForOption(_outputDirectoryOption);
        context.ExitCode = Resource!.Manifest(outputDir) ? 0 : 1;
    }
}
