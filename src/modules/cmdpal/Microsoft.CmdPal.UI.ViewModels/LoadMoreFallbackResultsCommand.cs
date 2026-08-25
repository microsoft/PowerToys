// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed partial class LoadMoreFallbackResultsCommand : InvokableCommand
{
    private Action? _loadMore;

    internal LoadMoreFallbackResultsCommand(string sourceId, Action loadMore)
    {
        _loadMore = loadMore;
        Id = $"com.microsoft.cmdpal.fallback.load-more:{sourceId}";
        Name = Properties.Resources.fallback_load_more;
    }

    public override ICommandResult Invoke()
    {
        Interlocked.Exchange(ref _loadMore, null)?.Invoke();
        return CommandResult.KeepOpen();
    }
}
