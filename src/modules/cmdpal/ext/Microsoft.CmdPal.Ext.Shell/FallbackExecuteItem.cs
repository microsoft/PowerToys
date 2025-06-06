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
    public bool FoundExecutable { get; private set; }

    public FallbackExecuteItem(SettingsManager settings)
        : base(new NoOpCommand(), Resources.shell_command_display_title)
    {
        Title = string.Empty;

        Icon = Icons.RunV2;
    }

    public override void UpdateQuery(string query)
    {
        var searchText = query.Trim();
        var expanded = Environment.ExpandEnvironmentVariables(searchText);
        searchText = expanded;
        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            Command = null;
            Title = string.Empty;
            FoundExecutable = false;
            return;
        }

        ShellListPage.ParseExecutableAndArgs(searchText, out var exe, out var args);
        var exeExists = ShellListPageHelpers.FileExistInPath(exe, out var fullExePath);
        var pathIsDir = Directory.Exists(exe);
        Debug.WriteLine($"Run: exeExists={exeExists}, pathIsDir={pathIsDir}");

        if (exeExists)
        {
            var exeItem = ShellListPage.CreateExeItems(exe, args, fullExePath);
            Title = exeItem.Title;
            Subtitle = exeItem.Subtitle;
            Icon = exeItem.Icon;
            Command = exeItem.Command;
            MoreCommands = exeItem.MoreCommands;
            FoundExecutable = true;
        }
        else if (pathIsDir)
        {
            var pathItem = new PathListItem(exe, query);
            Title = pathItem.Title;
            Subtitle = pathItem.Subtitle;
            Icon = pathItem.Icon;
            Command = pathItem.Command;
            MoreCommands = pathItem.MoreCommands;

            FoundExecutable = true;
        }
        else
        {
            Command = null;
            Title = string.Empty;
            FoundExecutable = false;
        }
    }

    internal static bool SuppressFileFallbackIf(string query)
    {
        var searchText = query.Trim();
        var expanded = Environment.ExpandEnvironmentVariables(searchText);
        searchText = expanded;
        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            return false;
        }

        ShellListPage.ParseExecutableAndArgs(searchText, out var exe, out var args);
        var exeExists = ShellListPageHelpers.FileExistInPath(exe, out var fullExePath);
        var pathIsDir = Directory.Exists(exe);

        return exeExists || pathIsDir;
    }
}
