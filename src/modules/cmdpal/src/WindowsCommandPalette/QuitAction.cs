// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;
using Windows.Foundation;

namespace DeveloperCommandPalette;

public class QuitAction : InvokableCommand, IFallbackHandler
{
    public QuitAction()
    {
        Icon = new("\uE711");
    }
    public override ICommandResult Invoke() {

        // Exit the application
        Environment.Exit(0);
        // unreachable
        return ActionResult.Dismiss();
    }
    public void UpdateQuery(string query) {
        if (query.StartsWith('q'))
        {
            this.Name = "Quit";
        }
        else this.Name = "";

    }
}
public class QuitActionProvider : ICommandProvider
{
    public string DisplayName => "";
    public IconDataType Icon => new("");
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    private readonly ListItem quitAction = new(new QuitAction()) { Subtitle = "Exit Run" };

    public IListItem[] TopLevelCommands()
    {
        return [quitAction];
    }
}
