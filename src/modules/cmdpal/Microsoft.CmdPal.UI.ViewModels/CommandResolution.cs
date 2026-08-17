// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class CommandResolution : IDisposable
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

    public void Dispose() => Interlocked.Exchange(ref _ownedCommand, null)?.Cleanup();
}
