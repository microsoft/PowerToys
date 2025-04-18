// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Shell.Commands;
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
        ListItem result = new ListItem(new ExecuteItem(cmd, _settings))
        {
            Title = cmd,
            Subtitle = Properties.Resources.cmd_plugin_name + ": " + Properties.Resources.cmd_execute_through_shell,
            Icon = new IconInfo(string.Empty),
        };

        return result;
    }

    private List<ListItem> GetHistoryCmds(string cmd, ListItem result)
    {
        IEnumerable<ListItem?> history = _settings.Count.Where(o => o.Key.Contains(cmd, StringComparison.CurrentCultureIgnoreCase))
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

        List<ListItem> results = new List<ListItem>();
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
        IEnumerable<ListItem> history = _settings.Count.OrderByDescending(o => o.Value)
            .Select(m => new ListItem(new ExecuteItem(m.Key, _settings))
            {
                Title = m.Key,

                // Using CurrentCulture since this is user facing
                Subtitle = Properties.Resources.cmd_plugin_name + ": " + string.Format(CultureInfo.CurrentCulture, CmdHasBeenExecutedTimes, m.Value),
                Icon = new IconInfo("\uE81C"),
            }).Take(5);

        return history.ToList();
    }
}
