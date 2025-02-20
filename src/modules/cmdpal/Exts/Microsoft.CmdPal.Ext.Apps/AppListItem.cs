// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
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

        MoreCommands = _app.Commands!.ToArray();
    }

    private void BuildDetails()
    {
        var metadata = new List<DetailsElement>();
        metadata.Add(new DetailsElement() { Key = "Type", Data = new DetailsTags() { Tags = [new Tag(_app.Type)] } });
        if (!_app.IsPackaged)
        {
            metadata.Add(new DetailsElement() { Key = "Path", Data = new DetailsLink() { Text = _app.ExePath } });
        }

        Details = new Details()
        {
            Title = this.Title,
            HeroImage = this.Icon ?? new IconInfo(string.Empty),
            Metadata = metadata.ToArray(),
        };
    }

    public async Task FetchIcon(bool useThumbnails)
    {
        if (_app.IsPackaged)
        {
            Icon = new IconInfo(_app.IcoPath);
            BuildDetails();
            return;
        }

        if (useThumbnails)
        {
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
        else
        {
            Icon = new IconInfo(_app.IcoPath);
        }

        BuildDetails();
    }
}
