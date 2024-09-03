// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;
using Windows.Foundation;

namespace WindowsCommandPalette.BuiltinCommands;

public class QuitAction : InvokableCommand, IFallbackHandler
{
    public event TypedEventHandler<object?, object?>? QuitRequested;

    public QuitAction()
    {
        Icon = new("\uE711");
    }

    public override ICommandResult Invoke()
    {
        QuitRequested?.Invoke(this, new());
        return ActionResult.KeepOpen();
    }

    public void UpdateQuery(string query)
    {
        if (query.StartsWith('q'))
        {
            Name = "Quit";
        }
        else
        {
            Name = string.Empty;
        }
    }
}

public class QuitActionProvider : ICommandProvider
{
    public string DisplayName => string.Empty;

    public IconDataType Icon => new(string.Empty);

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();

#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
    private readonly QuitAction quitAction = new();

    public event TypedEventHandler<object?, object?>? QuitRequested { add => quitAction.QuitRequested += value; remove => quitAction.QuitRequested -= value; }

    public IListItem[] TopLevelCommands()
    {
        return [new ListItem(quitAction) { Subtitle = "Exit Command Palette" }];
    }
}
