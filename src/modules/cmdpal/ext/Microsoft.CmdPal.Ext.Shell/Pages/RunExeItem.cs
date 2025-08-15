// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Shell.Pages;

internal sealed partial class RunExeItem : ListItem
{
    private readonly Lazy<IconInfo> _icon;
    private readonly Action<string>? _addToHistory;

    public override IIconInfo? Icon { get => _icon.Value; set => base.Icon = value; }

    internal string FullExePath { get; private set; }

    internal string Exe { get; private set; }

    private string _args = string.Empty;

    private string FullString => string.IsNullOrEmpty(_args) ? Exe : $"{Exe} {_args}";

    public RunExeItem(string exe, string args, string fullExePath, Action<string>? addToHistory)
    {
        FullExePath = fullExePath;
        Exe = exe;
        var command = new AnonymousCommand(Run)
        {
            Name = Properties.Resources.generic_run_command,
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

        UpdateArgs(args);

        MoreCommands = [
            new CommandContextItem(
                new AnonymousCommand(RunAsAdmin)
            {
                Name = Properties.Resources.cmd_run_as_administrator,
                Icon = Icons.AdminIcon,
            }) { RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.Enter) },
            new CommandContextItem(
                new AnonymousCommand(RunAsOther)
            {
                Name = Properties.Resources.cmd_run_as_user,
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
            if (stream != null)
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

        ShellHelpers.OpenInShell(FullExePath, _args);
    }

    public void RunAsAdmin()
    {
        _addToHistory?.Invoke(FullString);

        ShellHelpers.OpenInShell(FullExePath, _args, runAs: ShellHelpers.ShellRunAsType.Administrator);
    }

    public void RunAsOther()
    {
        _addToHistory?.Invoke(FullString);

        ShellHelpers.OpenInShell(FullExePath, _args, runAs: ShellHelpers.ShellRunAsType.OtherUser);
    }
}
