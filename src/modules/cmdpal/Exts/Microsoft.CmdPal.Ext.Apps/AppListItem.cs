// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

internal sealed partial class AppListItem : ListItem
{
    private readonly AppItem _app;
    private static readonly Tag _appTag = new("App");

    public AppListItem(AppItem app)
        : base(new AppCommand(app))
    {
        _app = app;
        Title = app.Name;
        Subtitle = app.Subtitle;
        Tags = [_appTag];

        Details = new Details()
        {
            Title = this.Title,
            HeroImage = ((AppCommand)Command!).Icon ?? new IconInfo(string.Empty),
            Body = "### " + app.Type,
        };

        MoreCommands = _app.Commands!.ToArray();
    }
}
