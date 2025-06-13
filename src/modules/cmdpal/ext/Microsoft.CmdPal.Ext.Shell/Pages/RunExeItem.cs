// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Shell.Pages;

internal sealed partial class RunExeItem : ListItem
{
    private readonly Lazy<IconInfo> _icon;

    public override IIconInfo? Icon { get => _icon.Value; set => base.Icon = value; }

    private readonly string _fullExePath;
    private readonly string _exe;
    private readonly string _args;

    public RunExeItem(string exe, string args, string fullExePath)
    {
        _fullExePath = fullExePath;
        _exe = exe;
        _args = args;
        var command = new AnonymousCommand(Run)
        {
            Name = "Run", // TODO:LOC
            Result = CommandResult.Dismiss(),
        };
        Command = command;
        Title = string.IsNullOrEmpty(args) ? exe : exe + " " + args; // todo! you're smarter than this
        Subtitle = _fullExePath;

        _icon = new Lazy<IconInfo>(() =>
        {
            var t = FetchIcon();
            t.Wait();
            return t.Result;
        });

        MoreCommands = [
            new CommandContextItem(
                new AnonymousCommand(RunAsAdmin)
            {
                Name = Properties.Resources.cmd_run_as_administrator,
                Icon = Icons.RunAsAdmin,
            }),
            new CommandContextItem(
                new AnonymousCommand(RunAsOther)
            {
                Name = Properties.Resources.cmd_run_as_user,
                Icon = Icons.RunAsUser,
            }),
        ];
    }

    public async Task<IconInfo> FetchIcon()
    {
        IconInfo? icon = null;

        try
        {
            var stream = await ThumbnailHelper.GetThumbnail(_fullExePath);
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

        icon = icon ?? new IconInfo(_fullExePath);
        return icon;
    }

    public void Run()
    {
        ShellHelpers.OpenInShell(_fullExePath, _args);
    }

    public void RunAsAdmin()
    {
        ShellHelpers.OpenInShell(_fullExePath, _args, runAs: ShellHelpers.ShellRunAsType.Administrator);
    }

    public void RunAsOther()
    {
        ShellHelpers.OpenInShell(_fullExePath, _args, runAs: ShellHelpers.ShellRunAsType.OtherUser);
    }
}
