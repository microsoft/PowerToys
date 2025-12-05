// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Shell.Pages;

internal sealed partial class RunExeItem : ListItem
{
    private readonly Lazy<IconInfo> _icon;
    private readonly Action<string>? _addToHistory;
    private readonly ITelemetryService? _telemetryService;

    public override IIconInfo? Icon { get => _icon.Value; set => base.Icon = value; }

    internal string FullExePath { get; private set; }

    internal string Exe { get; private set; }

    private string _args = string.Empty;

    private string FullString => string.IsNullOrEmpty(_args) ? Exe : $"{Exe} {_args}";

    public RunExeItem(
        string exe,
        string args,
        string fullExePath,
        Action<string>? addToHistory,
        ITelemetryService? telemetryService = null)
    {
        FullExePath = fullExePath;
        Exe = exe;
        var command = new AnonymousCommand(Run)
        {
            Name = ResourceLoaderInstance.GetString("generic_run_command"),
            Result = CommandResult.Dismiss(),
        };
        Command = command;
        Subtitle = FullExePath;

        _icon = new Lazy<IconInfo>(() =>
        {
            var t = FetchIcon();
            t.Wait();
            return t.Result;
        });

        _addToHistory = addToHistory;
        _telemetryService = telemetryService;

        UpdateArgs(args);

        MoreCommands = [
            new CommandContextItem(
                new AnonymousCommand(RunAsAdmin)
            {
                Name = ResourceLoaderInstance.GetString("cmd_run_as_administrator"),
                Icon = Icons.AdminIcon,
            }) { RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.Enter) },
            new CommandContextItem(
                new AnonymousCommand(RunAsOther)
            {
                Name = ResourceLoaderInstance.GetString("cmd_run_as_user"),
                Icon = Icons.UserIcon,
            }) { RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.U) },
        ];
    }

    internal void UpdateArgs(string args)
    {
        _args = args;
        Title = string.IsNullOrEmpty(_args) ? Exe : Exe + " " + _args; // todo! you're smarter than this
    }

    public async Task<IconInfo> FetchIcon()
    {
        IconInfo? icon = null;

        try
        {
            var stream = await ThumbnailHelper.GetThumbnail(FullExePath);
            if (stream is not null)
            {
                var data = new IconData(RandomAccessStreamReference.CreateFromStream(stream));
                icon = new IconInfo(data, data);
                ((AnonymousCommand?)Command)!.Icon = icon;
            }
        }
        catch
        {
        }

        icon = icon ?? new IconInfo(FullExePath);
        return icon;
    }

    public void Run()
    {
        _addToHistory?.Invoke(FullString);

        var success = ShellHelpers.OpenInShell(FullExePath, _args);

        _telemetryService?.LogRunCommand(FullString, false, success);
    }

    public void RunAsAdmin()
    {
        _addToHistory?.Invoke(FullString);

        var success = ShellHelpers.OpenInShell(FullExePath, _args, runAs: ShellHelpers.ShellRunAsType.Administrator);

        _telemetryService?.LogRunCommand(FullString, true, success);
    }

    public void RunAsOther()
    {
        _addToHistory?.Invoke(FullString);

        var success = ShellHelpers.OpenInShell(FullExePath, _args, runAs: ShellHelpers.ShellRunAsType.OtherUser);

        _telemetryService?.LogRunCommand(FullString, false, success);
    }
}
