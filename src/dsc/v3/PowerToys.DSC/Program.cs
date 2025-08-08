// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.DSC.Commands;
using PowerToys.DSC.Options;

namespace PowerToys.DSC;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("PowerToys Desired State Configuration CLI");
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
