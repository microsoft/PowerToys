// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.Extensions.Helpers;

namespace HackerNewsExtension;

internal sealed class CommentAction : InvokableCommand
{
    private readonly NewsPost _post;

    internal CommentAction(NewsPost post)
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
