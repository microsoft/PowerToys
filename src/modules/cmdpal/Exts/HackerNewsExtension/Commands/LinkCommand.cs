// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using HackerNewsExtension.Data;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HackerNewsExtension.Commands;

internal sealed partial class LinkCommand : InvokableCommand
{
    private readonly NewsPost _post;

    internal LinkCommand(NewsPost post)
    {
        _post = post;
        Name = "Open link";
        Icon = new IconInfo("\uE8A7");
    }

    public override CommandResult Invoke()
    {
        Process.Start(new ProcessStartInfo(_post.Link) { UseShellExecute = true });
        return CommandResult.KeepOpen();
    }
}
