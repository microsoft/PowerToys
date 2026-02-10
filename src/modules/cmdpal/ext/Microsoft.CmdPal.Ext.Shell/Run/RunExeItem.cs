// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using static Microsoft.CommandPalette.Extensions.Toolkit.ShellHelpers;

namespace Microsoft.CmdPal.Ext.Run;

internal sealed partial class RunExeItem : FileItem
{
    // FileItem takes care of our Icon for us.
    private readonly Action<string>? _addToHistory;
    private readonly ITelemetryService? _telemetryService;

    internal string FullExePath { get; private set; }

    internal string Exe { get; private set; }

    private string _args = string.Empty;

    private string FullString => string.IsNullOrEmpty(_args) ? Exe : $"{Exe} {_args}";

    // We're using a lazy to populate the commands once, only once they're
    // asked for.
    // You could also just do something like
    // public override IContextItem[] MoreCommands => BuildContextMenu();
    // to build it every time it is asked for, but only when asked
    private readonly Lazy<IContextItem[]> _lazyMoreCommands;

    public override IContextItem[] MoreCommands => _lazyMoreCommands.Value;

    public RunExeItem(
        string exe,
        string args,
        string fullExePath,
        Action<string>? addToHistory,
        ITelemetryService? telemetryService = null)
        : base(fullPath: fullExePath, isDirectory: false)
    {
        FullExePath = fullExePath;
        Exe = exe;
        Subtitle = FullExePath;

        _addToHistory = addToHistory;
        _telemetryService = telemetryService;

        UpdateArgs(args);

        // Use a lazy to populate the MoreCommands for Run items
        _lazyMoreCommands = new Lazy<IContextItem[]>(() =>
        {
            return BuildContextMenu();
        });
    }

    internal void UpdateArgs(string args)
    {
        _args = args;
        Title = string.IsNullOrEmpty(_args) ? Exe : Exe + " " + _args; // todo! you're smarter than this
    }

    public void Run()
    {
        var success = OpenInShell(FullExePath, _args);
        if (success)
        {
            _addToHistory?.Invoke(FullString);
        }

        _telemetryService?.LogRunCommand(FullString, false, success);
    }

    public void RunAsAdmin()
    {
        var success = OpenInShell(FullExePath, _args, runAs: ShellHelpers.ShellRunAsType.Administrator);
        if (success)
        {
            _addToHistory?.Invoke(FullString);
        }

        _telemetryService?.LogRunCommand(FullString, true, success);
    }

    public void RunAsOther()
    {
        var success = OpenInShell(FullExePath, _args, runAs: ShellHelpers.ShellRunAsType.OtherUser);
        if (success)
        {
            _addToHistory?.Invoke(FullString);
        }

        _telemetryService?.LogRunCommand(FullString, false, success);
    }

    private IContextItem[] BuildContextMenu()
    {
        // For debugging purposes, set this conditional to true to see what
        // context menu items on Run looks like.
#if false
#if !DEBUG
#error Do not check this in - CI should explode here
#endif
        // n.b. It might be a fun idea to use the StorageItem.GetMenuItemsAsync
        // APIs to build context menu items for run items
        List<IContextItem> items = new();

        // danger: filesystem access is potentially slow
        var isDir = Directory.Exists(FullExePath);

        // wrong, but ported from PT cmdpal:
        // Add runas commands only for files, not dirs
        // Problem is, this only applies to executables, not like word docs
        if (!isDir)
        {

            items.Add(new CommandContextItem(
                new AnonymousCommand(RunAsAdmin)
                {
                    Name = ResourceLoaderInstance.GetString("Run_run_as_administrator"),
                    Icon = Icons.AdminIcon,
                }));
            items.Add(
            new CommandContextItem(
                new AnonymousCommand(RunAsOther)
                {
                    Name = ResourceLoaderInstance.GetString("Run_run_as_user"),
                    Icon = Icons.UserIcon,
                }));
            items.Add(
            new Separator());
        }

        //items.Add(new CommandContextItem(new OpenWithCommand(FullExePath)));
        items.Add(new CommandContextItem(new ShowFileInFolderCommand(FullExePath)));
        items.Add(new CommandContextItem(new CopyPathCommand(FullExePath)));
        //items.Add(new CommandContextItem(new Common.Commands.OpenInConsoleCommand(FullExePath)));
        items.Add(new CommandContextItem(new Common.Commands.OpenPropertiesCommand(FullExePath)));

        return items.ToArray();
#else
        return [];
#endif
    }
}
