// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CmdPal.Ext.Shell.Commands;
using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

internal sealed partial class FallbackExecuteItem : FallbackCommandItem
{
    private readonly ExecuteItem _executeItem;
    private readonly SettingsManager _settings;

    public FallbackExecuteItem(SettingsManager settings)
        : base(new ExecuteItem(string.Empty, settings), Resources.shell_command_display_title)
    {
        _settings = settings;
        _executeItem = (ExecuteItem)this.Command!;
        Title = string.Empty;
        _executeItem.Name = string.Empty;
        Subtitle = Properties.Resources.generic_run_command;
        Icon = Icons.RunV2; // Defined in Icons.cs and contains the execute command icon.
    }

    public override void UpdateQuery(string query)
    {
        // Check if the query is a valid path to an exe/file
        var isValidPath = IsValidPath(query);

        // if not, let's bounce
        if (!isValidPath)
        {
            Title = string.Empty;
            MoreCommands = [];
            return;
        }

        // if so, proceed good sir
        _executeItem.Cmd = query;
        _executeItem.Name = string.IsNullOrEmpty(query) ? string.Empty : Properties.Resources.generic_run_command;
        Title = query;
        MoreCommands = [
            new CommandContextItem(new ExecuteItem(query, _settings, RunAsType.Administrator)),
            new CommandContextItem(new ExecuteItem(query, _settings, RunAsType.OtherUser)),
        ];
    }

    /// <summary>
    /// Checks if the provided string is a valid path to a file or executable
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>True if the path is valid, false otherwise</returns>
    private bool IsValidPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            // Check if it's a valid absolute path that exists
            if (File.Exists(path))
            {
                return true;
            }

            // Check if the command exists in PATH
            if (ExistsInPath(path))
            {
                return true;
            }

            // Check if it's a valid path with arguments
            var parts = path.Split(' ', 2);
            if (parts.Length > 0)
            {
                var executable = parts[0];

                // Check if the executable part exists directly or in PATH
                if (File.Exists(executable) || ExistsInPath(executable))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // Path was malformed or caused an exception during validation
            return false;
        }
    }

    /// <summary>
    /// Checks if a file exists in any of the PATH directories
    /// </summary>
    /// <param name="filename">The filename to check</param>
    /// <returns>True if the file exists in PATH, false otherwise</returns>
    private bool ExistsInPath(string filename)
    {
        if (File.Exists(filename))
        {
            return true;
        }
        else
        {
            var values = Environment.GetEnvironmentVariable("PATH");
            if (values != null)
            {
                foreach (var path in values.Split(';'))
                {
                    var path1 = Path.Combine(path, filename);
                    var path2 = Path.Combine(path, filename + ".exe");
                    if (File.Exists(path1) || File.Exists(path2))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
