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
        rootCommand.AddCommand(new GetCommand());
        rootCommand.AddCommand(new SetCommand());
        rootCommand.AddCommand(new ExportCommand());
        rootCommand.AddCommand(new TestCommand());
        rootCommand.AddCommand(new SchemaCommand());
        rootCommand.AddCommand(new ManifestCommand());
        rootCommand.AddCommand(new ModulesCommand());
        return await rootCommand.InvokeAsync(args);
    }
}
