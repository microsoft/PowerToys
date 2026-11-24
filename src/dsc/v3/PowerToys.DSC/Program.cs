// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using PowerToys.DSC.Commands;

namespace PowerToys.DSC;

/// <summary>
/// Main entry point for the PowerToys Desired State Configuration CLI application.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand(Properties.Resources.PowerToysDSC);
        rootCommand.Subcommands.Add(new GetCommand());
        rootCommand.Subcommands.Add(new SetCommand());
        rootCommand.Subcommands.Add(new ExportCommand());
        rootCommand.Subcommands.Add(new TestCommand());
        rootCommand.Subcommands.Add(new SchemaCommand());
        rootCommand.Subcommands.Add(new ManifestCommand());
        rootCommand.Subcommands.Add(new ModulesCommand());
        return await rootCommand.Parse(args).InvokeAsync();
    }
}
