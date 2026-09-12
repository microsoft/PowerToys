// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class CommandResolution : IAsyncDisposable
{
    private TopLevelViewModel? _ownedCommand;

    public TopLevelViewModel Command { get; }

    public CommandProviderWrapper Provider { get; }

    internal CommandResolution(TopLevelViewModel command, CommandProviderWrapper provider, bool ownsCommand)
    {
        Command = command;
        Provider = provider;
        _ownedCommand = ownsCommand ? command : null;
    }

    public ValueTask DisposeAsync()
    {
        var command = Interlocked.Exchange(ref _ownedCommand, null);
        return command is null ? ValueTask.CompletedTask : new ValueTask(Task.Run(command.Cleanup));
    }
}
