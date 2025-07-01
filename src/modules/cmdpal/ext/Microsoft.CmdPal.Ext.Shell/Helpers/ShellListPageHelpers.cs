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
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;

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

    internal static async Task<bool> FileExistInPathAsync(string filename, CancellationToken cancellationToken = default)
    {
        var (exists, _) = await FileExistInPathGetPathAsync(filename, cancellationToken);
        return exists;
    }

    internal static async Task<(bool Exists, string FullPath)> FileExistInPathGetPathAsync(string filename, CancellationToken cancellationToken = default)
    {
        var fullPath = string.Empty;

        // var expanded = Environment.ExpandEnvironmentVariables(filename);
        // Debug.WriteLine($"Run: filename={filename} -> expanded={expanded}");
        if (await FileExistsFastAsync(filename, cancellationToken: cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            fullPath = Path.GetFullPath(filename);
            return (true, fullPath);
        }
        else
        {
            var values = Environment.GetEnvironmentVariable("PATH");
            if (values != null)
            {
                foreach (var path in values.Split(';'))
                {
                    var path1 = Path.Combine(path, filename);
                    if (await FileExistsFastAsync(path1, cancellationToken: cancellationToken))
                    {
                        fullPath = Path.GetFullPath(path1);
                        return (true, fullPath);
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    var path2 = Path.Combine(path, filename + ".exe");
                    if (await FileExistsFastAsync(path2, cancellationToken: cancellationToken))
                    {
                        fullPath = Path.GetFullPath(path2);
                        return (true, fullPath);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }

                return (false, fullPath);
            }
            else
            {
                return (false, fullPath);
            }
        }
    }

    internal static async Task<bool> FileExistsFastAsync(string path, int timeoutMs = 200, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

        try
        {
            return await Task.Run(() => FileExistsInternal(path, linkedCts.Token), linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (timeoutCts.IsCancellationRequested && cancellationToken.IsCancellationRequested)
            {
                Debug.WriteLine("Cancelled: Both timeout and caller requested.");
            }
            else if (timeoutCts.IsCancellationRequested)
            {
                Debug.WriteLine("Cancelled: Timeout expired.");
            }
            else if (cancellationToken.IsCancellationRequested)
            {
                Debug.WriteLine("Cancelled: Caller token triggered.");
            }
            else
            {
                Debug.WriteLine("Cancelled: Unknown reason.");
            }

            return false;
        }
    }

    private static bool FileExistsInternal(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var handle = PInvoke.CreateFile(
            path,
            (uint)FILE_ACCESS_RIGHTS.FILE_READ_ATTRIBUTES,
            FILE_SHARE_MODE.FILE_SHARE_READ,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS,
            null);

        return !handle.IsInvalid;
    }

    internal static bool FileExistInPath(string filename)
    {
        return FileExistInPathAsync(filename).GetAwaiter().GetResult();
    }

    internal static bool FileExistInPathBlocking(string filename, out string fullPath, CancellationToken? token = null)
    {
        var cancellationToken = token ?? CancellationToken.None;
        var (exists, path) = FileExistInPathGetPathAsync(filename, cancellationToken).GetAwaiter().GetResult();
        fullPath = path;
        return exists;
    }
}
