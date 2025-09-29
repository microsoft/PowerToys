// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Ext.Shell.Commands;
using Microsoft.CmdPal.Ext.Shell.Pages;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell.Helpers;

public class ShellListPageHelpers
{
    private static readonly CompositeFormat CmdHasBeenExecutedTimes = System.Text.CompositeFormat.Parse(ResourceLoaderInstance.GetString("cmd_has_been_executed_times"));
    private readonly ISettingsInterface _settings;

    public ShellListPageHelpers(ISettingsInterface settings)
    {
        _settings = settings;
    }

    private ListItem GetCurrentCmd(string cmd)
    {
        var result = new ListItem(new ExecuteItem(cmd, _settings))
        {
            Title = cmd,
            Subtitle = ResourceLoaderInstance.GetString("cmd_plugin_name") + ": " + ResourceLoaderInstance.GetString("cmd_execute_through_shell"),
            Icon = new IconInfo(string.Empty),
        };

        return result;
    }

    public List<CommandContextItem> LoadContextMenus(ListItem listItem)
    {
        var resultList = new List<CommandContextItem>
            {
                new(new ExecuteItem(listItem.Title, _settings, RunAsType.Administrator)),
                new(new ExecuteItem(listItem.Title, _settings, RunAsType.OtherUser )),
            };

        return resultList;
    }

    internal static bool FileExistInPath(string filename)
    {
        return FileExistInPath(filename, out var _);
    }

    internal static bool FileExistInPath(string filename, out string fullPath, CancellationToken? token = null)
    {
        // TODO! remove this method and just use ShellHelpers.FileExistInPath directly
        return ShellHelpers.FileExistInPath(filename, out fullPath, token ?? CancellationToken.None);
    }

    internal static ListItem? ListItemForCommandString(string query, Action<string>? addToHistory, ITelemetryService? telemetryService)
    {
        var li = new ListItem();

        var searchText = query.Trim();
        var expanded = Environment.ExpandEnvironmentVariables(searchText);
        searchText = expanded;
        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            return null;
        }

        ShellHelpers.ParseExecutableAndArgs(searchText, out var exe, out var args);

        var exeExists = false;
        var pathIsDir = false;
        var fullExePath = string.Empty;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            // Use Task.Run with timeout - this will actually timeout even if the sync operations don't respond to cancellation
            var pathResolutionTask = Task.Run(
                () =>
            {
                // Don't check cancellation token here - let the Task timeout handle it
                exeExists = ShellListPageHelpers.FileExistInPath(exe, out fullExePath);
                pathIsDir = Directory.Exists(expanded);
            },
                CancellationToken.None); // Use None here since we're handling timeout differently

            // Wait for either completion or timeout
            pathResolutionTask.Wait(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        if (exeExists)
        {
            // TODO we need to probably get rid of the settings for this provider entirely
            var exeItem = ShellListPage.CreateExeItem(exe, args, fullExePath, addToHistory, telemetryService);
            li.Command = exeItem.Command;
            li.Title = exeItem.Title;
            li.Subtitle = exeItem.Subtitle;
            li.Icon = exeItem.Icon;
            li.MoreCommands = exeItem.MoreCommands;
        }
        else if (pathIsDir)
        {
            var pathItem = new PathListItem(exe, query, addToHistory, telemetryService);
            li.Command = pathItem.Command;
            li.Title = pathItem.Title;
            li.Subtitle = pathItem.Subtitle;
            li.Icon = pathItem.Icon;
            li.MoreCommands = pathItem.MoreCommands;
        }
        else if (System.Uri.TryCreate(searchText, UriKind.Absolute, out var uri))
        {
            li.Command = new OpenUrlWithHistoryCommand(searchText, addToHistory, telemetryService) { Result = CommandResult.Dismiss() };
            li.Title = searchText;
        }
        else
        {
            return null;
        }

        if (li is not null)
        {
            li.TextToSuggest = searchText;
        }

        return li;
    }

    /// <summary>
    /// This is a version of ParseExecutableAndArgs that handles whitespace in
    /// paths better. It will try to find the first matching executable in the
    /// input string.
    ///
    /// If the input is quoted, it will treat everything inside the quotes as
    /// the executable. If the input is not quoted, it will try to find the
    /// first segment that matches
    /// </summary>
    public static void NormalizeCommandLineAndArgs(string input, out string executable, out string arguments)
    {
        var normalized = CommandLineNormalizer.NormalizeCommandLine(input);
        var segments = normalized.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        executable = string.Empty;
        arguments = string.Empty;
        if (segments.Length == 0)
        {
            return;
        }

        executable = segments[0];
        if (segments.Length > 1)
        {
            arguments = string.Join(' ', segments[1..]);
        }
    }
}
