// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

internal sealed partial class AppListItem : ListItem
{
    private readonly AppItem _app;
    private static readonly Tag _appTag = new("App");

    private readonly Lazy<Details> _details;
    private readonly Lazy<IconInfo> _icon;

    public override IDetails? Details { get => _details.Value; set => base.Details = value; }

    public override IIconInfo? Icon { get => _icon.Value; set => base.Icon = value; }

    public string AppIdentifier { get; private set; }

    public AppListItem(AppItem app, bool useThumbnails)
        : base(new AppCommand(app))
    {
        _app = app;
        AppIdentifier = app.AppIdentifier;
        Title = app.Name;
        Subtitle = app.Subtitle;
        Tags = [_appTag];
        MoreCommands = _app.Commands!.ToArray();

        _details = new Lazy<Details>(() =>
        {
            var t = BuildDetails();
            t.Wait();
            return t.Result;
        });

        _icon = new Lazy<IconInfo>(() =>
        {
            var t = FetchIcon(useThumbnails);
            t.Wait();
            return t.Result;
        });
    }

    private async Task<Details> BuildDetails()
    {
        // Build metadata, with app type, path, etc.
        var metadata = new List<DetailsElement>();
        metadata.Add(new DetailsElement() { Key = "Type", Data = new DetailsTags() { Tags = [new Tag(_app.Type)] } });
        if (!_app.IsPackaged)
        {
            metadata.Add(new DetailsElement() { Key = "Path", Data = new DetailsLink() { Text = _app.ExePath } });
        }

        // Icon
        IconInfo? heroImage = null;
        if (_app.IsPackaged)
        {
            heroImage = new IconInfo(_app.IcoPath);
        }
        else
        {
            try
            {
                var stream = await ThumbnailHelper.GetThumbnail(_app.ExePath, true);
                if (stream != null)
                {
                    heroImage = IconInfo.FromStream(stream);
                }
            }
            catch (Exception)
            {
                // do nothing if we fail to load an icon.
                // Logging it would be too NOISY, there's really no need.
            }
        }

        return new Details()
        {
            Title = this.Title,
            HeroImage = heroImage ?? this.Icon ?? new IconInfo(string.Empty),
            Metadata = metadata.ToArray(),
        };
    }

    public async Task<IconInfo> FetchIcon(bool useThumbnails)
    {
        IconInfo? icon = null;
        if (_app.IsPackaged)
        {
            icon = new IconInfo(_app.IcoPath);
            return icon;
        }

        if (useThumbnails)
        {
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

            icon = icon ?? new IconInfo(_app.IcoPath);
        }
        else
        {
            icon = new IconInfo(_app.IcoPath);
        }

        return icon;
    }
}
