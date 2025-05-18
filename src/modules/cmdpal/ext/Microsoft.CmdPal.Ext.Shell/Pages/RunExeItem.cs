// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Shell.Pages;

internal sealed partial class RunExeItem : ListItem
{
    private readonly Lazy<IconInfo> _icon;

    public override IIconInfo? Icon { get => _icon.Value; set => base.Icon = value; }

    private readonly string _exe;
    private readonly string _args;

    public RunExeItem(string exe, string args)
    {
        _exe = exe;
        _args = args;
        var command = new AnonymousCommand(() => { ShellHelpers.OpenInShell(exe, args); })
        {
            Name = "Run", // TODO:LOC
            Result = CommandResult.Dismiss(),
        };
        Command = command;
        Title = string.IsNullOrEmpty(args) ? exe : exe + " " + args; // todo! you're smarter than this
        Subtitle = Path.GetFullPath(exe);

        _icon = new Lazy<IconInfo>(() =>
        {
            var t = FetchIcon();
            t.Wait();
            return t.Result;
        });
    }

    public async Task<IconInfo> FetchIcon()
    {
        IconInfo? icon = null;

        try
        {
            var stream = await ThumbnailHelper.GetThumbnail(_exe);
            if (stream != null)
            {
                var data = new IconData(RandomAccessStreamReference.CreateFromStream(stream));
                icon = new IconInfo(data, data);
            }
        }
        catch
        {
        }

        icon = icon ?? new IconInfo(_exe);
        return icon;
    }
}
