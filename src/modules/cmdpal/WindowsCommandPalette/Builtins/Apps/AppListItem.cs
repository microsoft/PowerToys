// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions.Helpers;

namespace WindowsCommandPalette.BuiltinCommands.AllApps;

internal sealed class AppListItem : ListItem
{
    private readonly AppItem _app;

    public AppListItem(AppItem app)
        : base(new AppAction(app))
    {
        _app = app;
        Title = app.Name;
        Subtitle = app.Subtitle;
        Tags = [new Tag() { Text = "App" }];

        Details = new Details()
        {
            Title = this.Title,
            HeroImage = Command?.Icon ?? new(string.Empty),
            Body = "### App",
        };

        if (string.IsNullOrEmpty(app.UserModelId))
        {
            // Win32 exe or other non UWP app
            MoreCommands = [
                new CommandContextItem(
                    new OpenPathAction(app.DirPath)
                    {
                        Name = "Open location",
                        Icon = new("\ue838"),
                    })
            ];
        }
        else
        {
            // UWP app
            MoreCommands = [];
        }
    }
}
