// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Commands;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Ext.Shell;
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

        var command = new AnonymousCommand(Run)
        {
            Name = ResourceLoaderInstance.GetString("generic_run_command"),
            Result = CommandResult.Dismiss(),
        };
        Command = command;

        // Use a lazy to populate the MoreCommands for Run items
        _lazyMoreCommands = new Lazy<IContextItem[]>(() =>
        {
            return BuildContextMenu(
                FullExePath,
                new AnonymousCommand(RunAsAdmin)
                {
                    Name = ResourceLoaderInstance.GetString("Run_run_as_administrator"),
                    Icon = Icons.AdminIcon,
                },
                new AnonymousCommand(RunAsOther)
                {
                    Name = ResourceLoaderInstance.GetString("Run_run_as_user"),
                    Icon = Icons.UserIcon,
                });
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

    internal static IContextItem[] BuildContextMenu(
        string fullExePath,
        ICommand? runAsAdminCommand,
        ICommand? runAsOtherUserCommand)
    {
        List<IContextItem> items = new();

        // danger: filesystem access is potentially slow
        var isDir = Directory.Exists(fullExePath);

        // Add runas commands only for files, not dirs
        if (!isDir)
        {
            if (runAsAdminCommand is not null)
            {
                items.Add(new CommandContextItem(runAsAdminCommand));
            }

            if (runAsOtherUserCommand is not null)
            {
                items.Add(new CommandContextItem(runAsOtherUserCommand));
            }

            if (runAsAdminCommand is not null || runAsOtherUserCommand is not null)
            {
                items.Add(new Separator());
            }
        }

        items.Add(new CommandContextItem(new OpenWithCommand(fullExePath)));
        items.Add(new CommandContextItem(new ShowFileInFolderCommand(fullExePath)));
        items.Add(new CommandContextItem(new CopyPathCommand(fullExePath)));
        items.Add(new CommandContextItem(new OpenInConsoleCommand(fullExePath)));
        items.Add(new CommandContextItem(new OpenPropertiesCommand(fullExePath)));

        return items.ToArray();
    }
}

#pragma warning disable SA1402 // File may only contain a single type
internal enum RunType
{
    Normal,
    AsAdmin,
    AsOtherUser,
}

internal sealed partial class RunCommandCommand : InvokableCommand
{
    private readonly string _commandline;
    private readonly RunType _runAs;
    private readonly Action<string>? _addToHistory;
    private readonly ITelemetryService? _telemetryService;

    public RunCommandCommand(
        string commandline,
        RunType runAs,
        Action<string>? addToHistory,
        ITelemetryService? telemetryService = null)
    {
        _commandline = commandline;
        _runAs = runAs;
        _addToHistory = addToHistory;
        _telemetryService = telemetryService;

        Name = runAs switch
        {
            RunType.Normal => ResourceLoaderInstance.GetString("generic_run_command"),
            RunType.AsAdmin => ResourceLoaderInstance.GetString("Run_run_as_administrator"),
            RunType.AsOtherUser => ResourceLoaderInstance.GetString("Run_run_as_user"),
            _ => throw new InvalidOperationException(),
        };
        if (runAs == RunType.AsAdmin)
        {
            Icon = Icons.AdminIcon;
        }
        else if (runAs == RunType.AsOtherUser)
        {
            Icon = Icons.UserIcon;
        }

        // normal will be set on us by our owner
    }

    public override ICommandResult Invoke()
    {
        var hr = RunHistory.ExecuteCommandline(
            commandLine: _commandline,
            workingDirectory: string.Empty,
            hwnd: 0,
            runAsAdmin: _runAs == RunType.AsAdmin);

        var success = hr == 0;
        if (success)
        {
            _addToHistory?.Invoke(_commandline);
        }

        _telemetryService?.LogRunCommand(_commandline, _runAs == RunType.AsAdmin, success);
        return CommandResult.Dismiss();
    }
}

internal sealed partial class RunCommandLineItem : FileItem
{
    private readonly Action<string>? _addToHistory;
    private readonly ITelemetryService? _telemetryService;

    private readonly Lazy<IContextItem[]> _lazyMoreCommands;

    public override IContextItem[] MoreCommands => _lazyMoreCommands.Value;

    public RunCommandLineItem(
        string fullExePath,
        string commandline,
        Action<string>? addToHistory,
        ITelemetryService? telemetryService = null)
        : base(fullPath: fullExePath, isDirectory: null)
    {
        _addToHistory = addToHistory;
        _telemetryService = telemetryService;
        Title = ResourceLoaderInstance.GetString("Run_command_line_command_title");
        Subtitle = commandline;
        TextToSuggest = commandline;

        Command = new RunCommandCommand(commandline, RunType.Normal, _addToHistory, _telemetryService);

        // Use a lazy to populate the MoreCommands for Run items
        _lazyMoreCommands = new Lazy<IContextItem[]>(() =>
        {
            return RunExeItem.BuildContextMenu(
                fullExePath,
                new RunCommandCommand(commandline, RunType.AsAdmin, _addToHistory, _telemetryService),
                null);
        });
    }
}

#pragma warning restore SA1402 // File may only contain a single type
