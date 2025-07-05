// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Shell.Commands;
using Microsoft.CmdPal.Ext.Shell.Pages;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell.Helpers;

public class ShellListPageHelpers
{
    private static readonly CompositeFormat CmdHasBeenExecutedTimes = System.Text.CompositeFormat.Parse(Properties.Resources.cmd_has_been_executed_times);
    private readonly SettingsManager _settings;

    public ShellListPageHelpers(SettingsManager settings)
    {
        _settings = settings;
    }

    private ListItem GetCurrentCmd(string cmd)
    {
        var result = new ListItem(new ExecuteItem(cmd, _settings))
        {
            Title = cmd,
            Subtitle = Properties.Resources.cmd_plugin_name + ": " + Properties.Resources.cmd_execute_through_shell,
            Icon = new IconInfo(string.Empty),
        };

        return result;
    }

    private List<ListItem> GetHistoryCmds(string cmd, ListItem result)
    {
        var history = _settings.Count.Where(o => o.Key.Contains(cmd, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(o => o.Value)
            .Select(m =>
            {
                if (m.Key == cmd)
                {
                    // Using CurrentCulture since this is user facing
                    result.Subtitle = Properties.Resources.cmd_plugin_name + ": " + string.Format(CultureInfo.CurrentCulture, CmdHasBeenExecutedTimes, m.Value);
                    return null;
                }

                var ret = new ListItem(new ExecuteItem(m.Key, _settings))
                {
                    Title = m.Key,

                    // Using CurrentCulture since this is user facing
                    Subtitle = Properties.Resources.cmd_plugin_name + ": " + string.Format(CultureInfo.CurrentCulture, CmdHasBeenExecutedTimes, m.Value),
                    Icon = new IconInfo("\uE81C"),
                };
                return ret;
            }).Where(o => o != null).Take(4);
        return history.Select(o => o!).ToList();
    }

    public List<ListItem> Query(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var results = new List<ListItem>();
        var cmd = query;
        if (string.IsNullOrEmpty(cmd))
        {
            results = ResultsFromlHistory();
        }
        else
        {
            var queryCmd = GetCurrentCmd(cmd);
            results.Add(queryCmd);
            var history = GetHistoryCmds(cmd, queryCmd);
            results.AddRange(history);
        }

        foreach (var currItem in results)
        {
            currItem.MoreCommands = LoadContextMenus(currItem).ToArray();
        }

        return results;
    }

    public List<CommandContextItem> LoadContextMenus(ListItem listItem)
    {
        var resultlist = new List<CommandContextItem>
            {
                new(new ExecuteItem(listItem.Title, _settings, RunAsType.Administrator)),
                new(new ExecuteItem(listItem.Title, _settings, RunAsType.OtherUser )),
            };

        return resultlist;
    }

    private List<ListItem> ResultsFromlHistory()
    {
        var history = _settings.Count.OrderByDescending(o => o.Value)
            .Select(m => new ListItem(new ExecuteItem(m.Key, _settings))
            {
                Title = m.Key,

                // Using CurrentCulture since this is user facing
                Subtitle = Properties.Resources.cmd_plugin_name + ": " + string.Format(CultureInfo.CurrentCulture, CmdHasBeenExecutedTimes, m.Value),
                Icon = new IconInfo("\uE81C"),
            }).Take(5);

        return history.ToList();
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

    internal static ListItem? ListItemForCommandString(string query, Action<string>? addToHistory)
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
            // pathResolutionTask.WaitAsync(cts.Token).ConfigureAwait(false);
            pathResolutionTask.Wait(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("Operation was canceled.");
        }

        Debug.WriteLine($"Run: exeExists={exeExists}, pathIsDir={pathIsDir}");

        if (exeExists)
        {
            // TODO we need to probably get rid of the settings for this provider entirely
            var exeItem = ShellListPage.CreateExeItem(exe, args, fullExePath, addToHistory);
            li.Command = exeItem.Command;
            li.Title = exeItem.Title;
            li.Subtitle = exeItem.Subtitle;
            li.Icon = exeItem.Icon;
            li.MoreCommands = exeItem.MoreCommands;
        }
        else if (pathIsDir)
        {
            var pathItem = new PathListItem(exe, query, addToHistory);
            li.Command = pathItem.Command;
            li.Title = pathItem.Title;
            li.Subtitle = pathItem.Subtitle;
            li.Icon = pathItem.Icon;
            li.MoreCommands = pathItem.MoreCommands;
        }
        else if (System.Uri.TryCreate(searchText, UriKind.Absolute, out var uri))
        {
            li.Command = new OpenUrlWithHistoryCommand(searchText) { Result = CommandResult.Dismiss() };
            li.Title = searchText;
        }
        else
        {
            return null;
        }

        return li;
    }
}
