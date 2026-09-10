// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.Threading.Tasks;
using PowerToys.Settings.Cli.Commands;

namespace PowerToys.Settings.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("PowerToys Settings CLI - Command line interface for PowerToys Settings");

        rootCommand.AddCommand(new ListCommand());
        rootCommand.AddCommand(new GetCommand());
        rootCommand.AddCommand(new SetCommand());
        rootCommand.AddCommand(new ToggleCommand());

        return await rootCommand.InvokeAsync(args);
    }
}
