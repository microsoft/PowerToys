// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using PowerToys.Settings.Cli.Helpers;

namespace PowerToys.Settings.Cli.Commands;

internal sealed class BackupCommand : Command
{
    public BackupCommand()
        : base("backup", "Backup PowerToys settings to a specified ZIP file or folder")
    {
        var outputOpt = new Option<string>("--output", "Destination path for backup (ZIP file or directory)") { IsRequired = true };
        outputOpt.AddAlias("-o");

        AddOption(outputOpt);

        this.SetHandler(
            (string output) =>
            {
                var exitCode = Execute(output);
                Environment.ExitCode = exitCode;
            },
            outputOpt);
    }

    private static int Execute(string output)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                Console.Error.WriteLine("Output path must be specified.");
                return 1;
            }

            SettingsCliHelper.BackupSettings(output);
            Console.WriteLine($"Settings successfully backed up to '{output}'.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to backup settings: {ex.Message}");
            return 1;
        }
    }
}
