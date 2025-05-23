// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CmdPal.Ext.Shell.Pages;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

internal sealed partial class FallbackExecuteItem : FallbackCommandItem
{
    // private readonly ExecuteItem _executeItem;
    public FallbackExecuteItem(SettingsManager settings)
        : base(new NoOpCommand(), Resources.shell_command_display_title)
    {
        // _executeItem = (ExecuteItem)this.Command!;
        Title = string.Empty;

        // _executeItem.Name = string.Empty;
        // Subtitle = Properties.Resources.generic_run_command;
        Icon = Icons.RunV2; // Defined in Icons.cs and contains the execute command icon.
    }

    public override void UpdateQuery(string query)
    {
        // _executeItem.Cmd = query;
        // _executeItem.Name = string.IsNullOrEmpty(query) ? string.Empty : Properties.Resources.generic_run_command;
        // Title = query;
        var searchText = query.Trim();
        var expanded = Environment.ExpandEnvironmentVariables(searchText);
        searchText = expanded;
        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            Command = null;
            Title = string.Empty;
            return;
        }

        ShellListPage.ParseExecutableAndArgs(searchText, out var exe, out var args);
        var exeExists = ShellListPageHelpers.FileExistInPath(exe, out var fullExePath);
        var pathIsDir = Directory.Exists(exe);
        Debug.WriteLine($"Run: exeExists={exeExists}, pathIsDir={pathIsDir}");
        if (exeExists)
        {
            var exeItem = ShellListPage.CreateExeItems(exe, args, fullExePath);
            this.Title = exeItem.Title;
            this.Subtitle = exeItem.Subtitle;
            this.Icon = exeItem.Icon;
            this.Command = exeItem.Command;
        }
        else if (pathIsDir)
        {
            var pathItem = new PathListItem(exe);
            this.Title = pathItem.Title;
            this.Subtitle = pathItem.Subtitle;
            this.Icon = pathItem.Icon;
            this.Command = pathItem.Command;
        }
        else
        {
            Command = null;
            Title = string.Empty;
        }
    }
}
