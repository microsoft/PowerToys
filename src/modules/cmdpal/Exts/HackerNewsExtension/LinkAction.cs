// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.Extensions.Helpers;

namespace HackerNewsExtension;

internal sealed class LinkAction : InvokableCommand
{
    private readonly NewsPost _post;

    internal LinkAction(NewsPost post)
    {
        this._post = post;
        this.Name = "Open link";
        this.Icon = new("\uE8A7");
    }

    public override CommandResult Invoke()
    {
        Process.Start(new ProcessStartInfo(_post.Link) { UseShellExecute = true });
        return CommandResult.KeepOpen();
    }
}
