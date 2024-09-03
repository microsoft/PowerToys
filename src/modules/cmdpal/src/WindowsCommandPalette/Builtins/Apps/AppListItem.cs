// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Windows.CommandPalette.Extensions.Helpers;

namespace WindowsCommandPalette.BuiltinCommands.AllApps;

internal sealed class AppListItem : ListItem
{
    private readonly AppItem app;
    public AppListItem(AppItem app) : base(new AppAction(app))
    {
        this.app = app;
        this.Title = app.Name;
        this.Subtitle = app.Subtitle;
        this.Details = new Details() { Title = this.Title, HeroImage = this.Command.Icon, Body = "### App" };
        this.Tags = [new Tag() { Text = "App" }];

        if (string.IsNullOrEmpty(app.UserModelId))
        {
            // Win32 exe or other non UWP app
            this._MoreCommands = [
                new CommandContextItem(new OpenPathAction(app.DirPath){ Name = "Open location", Icon=new("\ue838") })
            ];
        }
        else
        {
            // UWP app
            this._MoreCommands = [];
        }
    }
}
