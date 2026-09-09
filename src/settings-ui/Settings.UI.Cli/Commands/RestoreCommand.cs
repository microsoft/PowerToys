// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using PowerToys.Settings.Cli.Helpers;

namespace PowerToys.Settings.Cli.Commands;

internal sealed class RestoreCommand : Command
{
    public RestoreCommand()
        : base("restore", "Restore PowerToys settings from a specified ZIP file or folder")
    {
        var inputOpt = new Option<string>("--input", "Source path of backup (ZIP file or directory)") { IsRequired = true };
        inputOpt.AddAlias("-i");

        AddOption(inputOpt);

        this.SetHandler(
            (string input) =>
            {
                var exitCode = Execute(input);
                Environment.ExitCode = exitCode;
            },
            inputOpt);
    }

    private static int Execute(string input)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                Console.Error.WriteLine("Input path must be specified.");
                return 1;
            }

            SettingsCliHelper.RestoreSettings(input);
            Console.WriteLine($"Settings successfully restored from '{input}'.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to restore settings: {ex.Message}");
            return 1;
        }
    }
}
