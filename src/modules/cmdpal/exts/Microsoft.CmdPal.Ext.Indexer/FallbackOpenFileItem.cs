// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Indexer;

internal sealed partial class FallbackOpenFileItem : FallbackCommandItem
{
    public FallbackOpenFileItem()
        : base(new NoOpCommand(), Resources.Indexer_Find_Path_fallback_display_title)
    {
        Title = string.Empty;
        Subtitle = string.Empty;
    }

    public override void UpdateQuery(string query)
    {
        if (Path.Exists(query))
        {
            var item = new IndexerItem() { FullPath = query, FileName = Path.GetFileName(query) };
            var listItemForUs = new IndexerListItem(item, IncludeBrowseCommand.AsDefault);
            Command = listItemForUs.Command;
            MoreCommands = listItemForUs.MoreCommands;
            Subtitle = item.FileName;
            Title = item.FullPath;
            Icon = listItemForUs.Icon;

            try
            {
                var stream = ThumbnailHelper.GetThumbnail(item.FullPath).Result;
                if (stream != null)
                {
                    var data = new IconData(RandomAccessStreamReference.CreateFromStream(stream));
                    Icon = new IconInfo(data, data);
                }
            }
            catch
            {
            }
        }
        else
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            Command = new NoOpCommand();
        }
    }
}
