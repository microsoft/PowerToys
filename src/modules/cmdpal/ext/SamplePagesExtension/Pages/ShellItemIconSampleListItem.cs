// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace SamplePagesExtension.Pages;

internal sealed partial class ShellItemIconSampleListItem : ListItem
{
    public ShellItemIconSampleListItem(FileSystemInfo entry, bool useLegacyIndexerIcon)
        : base(new CopyTextCommand(entry.FullName) { Name = "Copy path" })
    {
        var isDirectory = entry is DirectoryInfo;
        var requestDescription = useLegacyIndexerIcon ? "legacy Indexer stream" : "semantic Shell icon request";
        Title = entry.Name;
        Subtitle = isDirectory
            ? $"Folder · {requestDescription}"
            : $"{(string.IsNullOrEmpty(entry.Extension) ? "No extension" : entry.Extension)} file · {requestDescription}";
        Icon = useLegacyIndexerIcon
            ? CreateLegacyIndexerIcon(entry.FullName)
            : new IconInfo(ShellItemIconProtocol.Create(entry.FullName));
    }

    private static IconInfo CreateLegacyIndexerIcon(string path)
    {
        try
        {
            // Match SearchEngine.FetchItems before ShellItemIconProtocol: Indexer synchronously
            // materialized each Shell thumbnail and sent it to CmdPal as an icon-data stream.
            var stream = ThumbnailHelper.GetThumbnail(path).Result;
            if (stream is null)
            {
                return null!;
            }

            var data = new IconData(RandomAccessStreamReference.CreateFromStream(stream));
            return new IconInfo(data, data);
        }
        catch
        {
            // Match the legacy Indexer failure behavior: publish the row without an icon.
            return null!;
        }
    }
}
