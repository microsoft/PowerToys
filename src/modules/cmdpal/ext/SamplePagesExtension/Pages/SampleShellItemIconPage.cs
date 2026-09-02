// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

internal sealed partial class SampleShellItemIconPage : ListPage
{
    private readonly bool _useLegacyIndexerIcons;
    private IListItem[] _items;

    public SampleShellItemIconPage(bool useLegacyIndexerIcons = false)
    {
        _useLegacyIndexerIcons = useLegacyIndexerIcons;
        Name = useLegacyIndexerIcons ? "System32 Legacy Indexer Icons" : "System32 Shell Icons";
        Icon = new IconInfo("\uE8B7"); // Folder
    }

    public override IListItem[] GetItems() => _items ??= CreateItems(_useLegacyIndexerIcons);

    private static IListItem[] CreateItems(bool useLegacyIndexerIcons)
    {
        try
        {
            var entries = new DirectoryInfo(Environment.SystemDirectory).GetFileSystemInfos();
            Array.Sort(entries, CompareEntries);

            var items = new IListItem[entries.Length];
            for (var index = 0; index < entries.Length; index++)
            {
                items[index] = new ShellItemIconSampleListItem(entries[index], useLegacyIndexerIcons);
            }

            return items;
        }
        catch (Exception exception)
        {
            return
            [
                new ListItem(new CopyTextCommand(exception.Message) { Name = "Copy error" })
                {
                    Title = "System32 could not be enumerated",
                    Subtitle = exception.Message,
                    Icon = new IconInfo("\uE783"),
                },
            ];
        }
    }

    private static int CompareEntries(FileSystemInfo left, FileSystemInfo right)
    {
        var leftIsDirectory = left is DirectoryInfo;
        var rightIsDirectory = right is DirectoryInfo;
        if (leftIsDirectory != rightIsDirectory)
        {
            return leftIsDirectory ? -1 : 1;
        }

        var extensionComparison = StringComparer.OrdinalIgnoreCase.Compare(left.Extension, right.Extension);
        return extensionComparison != 0
            ? extensionComparison
            : StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
    }
}
