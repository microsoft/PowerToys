// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
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
        var result = new ListItem(new ExecuteItem(cmd, _settings))
        {
            Title = cmd,
            Subtitle = Properties.Resources.cmd_plugin_name + ": " + Properties.Resources.cmd_execute_through_shell,
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
        fullPath = string.Empty;

        if (File.Exists(filename))
        {
            token?.ThrowIfCancellationRequested();
            fullPath = Path.GetFullPath(filename);
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
                    if (File.Exists(path1))
                    {
                        fullPath = Path.GetFullPath(path1);
                        return true;
                    }

                    token?.ThrowIfCancellationRequested();

                    var path2 = Path.Combine(path, filename + ".exe");
                    if (File.Exists(path2))
                    {
                        fullPath = Path.GetFullPath(path2);
                        return true;
                    }

                    token?.ThrowIfCancellationRequested();
                }

                return false;
            }
            else
            {
                return false;
            }
        }
    }
}
