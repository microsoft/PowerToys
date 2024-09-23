// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using HackerNewsExtension.Data;
using Microsoft.CmdPal.Extensions.Helpers;

namespace HackerNewsExtension.Commands;

internal sealed partial class CommentCommand : InvokableCommand
{
    private readonly NewsPost _post;

    internal CommentCommand(NewsPost post)
    {
        _post = post;
        Name = "Open comments";
        Icon = new("\ue8f2"); // chat bubbles
    }

    public override CommandResult Invoke()
    {
        Process.Start(new ProcessStartInfo(_post.CommentsLink) { UseShellExecute = true });
        return CommandResult.KeepOpen();
    }
}
