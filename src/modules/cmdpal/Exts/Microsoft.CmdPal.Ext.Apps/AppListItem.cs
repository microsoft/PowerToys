// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

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

    public async Task FetchIcon()
    {
        if (_app.IsPackaged)
        {
            Icon = new IconInfo(_app.IcoPath);
            return;
        }

        IconInfo? icon = null;
        try
        {
            var stream = await ThumbnailHelper.GetThumbnail(_app.ExePath);
            if (stream != null)
            {
                var data = new IconData(RandomAccessStreamReference.CreateFromStream(stream));
                icon = new IconInfo(data, data);
            }
        }
        catch
        {
        }

        Icon = icon ?? new IconInfo(_app.IcoPath);
    }
}
