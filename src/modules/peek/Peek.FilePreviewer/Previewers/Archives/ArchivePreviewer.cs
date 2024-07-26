// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Archives.Helpers;
using Peek.FilePreviewer.Previewers.Archives.Models;
using Peek.FilePreviewer.Previewers.Interfaces;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Peek.FilePreviewer.Previewers.Archives
{
    public partial class ArchivePreviewer : ObservableObject, IArchivePreviewer
    {
        private static readonly char[] _keySeparators = { '/', '\\' };

        private readonly IconCache _iconCache = new();
        private int _directoryCount;
        private int _fileCount;
        private ulong _size;
        private ulong _extractedSize;

        [ObservableProperty]
        private PreviewState state;

        [ObservableProperty]
        private string? _directoryCountText;

        [ObservableProperty]
        private string? _fileCountText;

        [ObservableProperty]
        private string? _sizeText;

        private IFileSystemItem Item { get; }

        private DispatcherQueue Dispatcher { get; }

        public ObservableCollection<ArchiveItem> Tree { get; }

        public ArchivePreviewer(IFileSystemItem file)
        {
            Item = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
            Tree = new ObservableCollection<ArchiveItem>();
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageItem = await Item.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
        }

        public Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new PreviewSize { MonitorSize = null });
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            State = PreviewState.Loading;
            using var stream = File.OpenRead(Item.Path);

            if (Item.Path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || Item.Path.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            {
                using var archive = ArchiveFactory.Open(stream);
                _extractedSize = (ulong)archive.TotalUncompressSize;
                stream.Seek(0, SeekOrigin.Begin);

                using var reader = ReaderFactory.Open(stream);
                while (reader.MoveToNextEntry())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await AddEntryAsync(reader.Entry, cancellationToken);
                }
            }
            else
            {
                using var archive = ArchiveFactory.Open(stream);
                _extractedSize = (ulong)archive.TotalUncompressSize;

                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await AddEntryAsync(entry, cancellationToken);
                }
            }

            _size = (ulong)new FileInfo(Item.Path).Length; // archive.TotalSize isn't accurate
            DirectoryCountText = string.Format(CultureInfo.CurrentCulture, ResourceLoaderInstance.ResourceLoader.GetString("Archive_Directory_Count"), _directoryCount);
            FileCountText = string.Format(CultureInfo.CurrentCulture, ResourceLoaderInstance.ResourceLoader.GetString("Archive_File_Count"), _fileCount);
            SizeText = string.Format(CultureInfo.CurrentCulture, ResourceLoaderInstance.ResourceLoader.GetString("Archive_Size"), ReadableStringHelper.BytesToReadableString(_size), ReadableStringHelper.BytesToReadableString(_extractedSize));

            State = PreviewState.Loaded;
        }

        public static bool IsItemSupported(IFileSystemItem item)
        {
            return _supportedFileTypes.Contains(item.Extension);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private async Task AddEntryAsync(IEntry entry, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(entry, nameof(entry));

            if (entry.Key == null)
            {
                return;
            }

            var levels = entry.Key.Split(_keySeparators, StringSplitOptions.RemoveEmptyEntries);

            ArchiveItem? parent = null;
            for (var i = 0; i < levels.Length; i++)
            {
                var type = (!entry.IsDirectory && i == levels.Length - 1) ? ArchiveItemType.File : ArchiveItemType.Directory;

                var icon = type == ArchiveItemType.Directory
                    ? await _iconCache.GetDirectoryIconAsync(cancellationToken)
                    : await _iconCache.GetFileExtIconAsync(entry.Key, cancellationToken);

                var item = new ArchiveItem(levels[i], type, icon);

                if (type == ArchiveItemType.Directory)
                {
                    item.IsExpanded = parent == null; // Only the root level is expanded
                }
                else if (type == ArchiveItemType.File)
                {
                    item.Size = (ulong)entry.Size;
                }

                if (parent == null)
                {
                    var existing = Tree.FirstOrDefault(e => e.Name == item.Name);
                    if (existing == null)
                    {
                        var index = GetIndex(Tree, item);
                        Tree.Insert(index, item);
                        CountItem(item);
                    }

                    parent = existing ?? Tree.First(e => e.Name == item.Name);
                }
                else
                {
                    var existing = parent.Children.FirstOrDefault(e => e.Name == item.Name);
                    if (existing == null)
                    {
                        var index = GetIndex(parent.Children, item);
                        parent.Children.Insert(index, item);
                        CountItem(item);
                    }

                    parent = existing ?? parent.Children.First(e => e.Name == item.Name);
                }
            }
        }

        private int GetIndex(ObservableCollection<ArchiveItem> collection, ArchiveItem item)
        {
            for (var i = 0; i < collection.Count; i++)
            {
                if (item.Type == collection[i].Type && string.Compare(collection[i].Name, item.Name, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    return i;
                }
            }

            return item.Type switch
            {
                ArchiveItemType.Directory => collection.Count(e => e.Type == ArchiveItemType.Directory),
                ArchiveItemType.File => collection.Count,
                _ => 0,
            };
        }

        private void CountItem(ArchiveItem item)
        {
            if (item.Type == ArchiveItemType.Directory)
            {
                _directoryCount++;
            }
            else if (item.Type == ArchiveItemType.File)
            {
                _fileCount++;
            }
        }

        private static readonly HashSet<string> _supportedFileTypes = new()
        {
            ".zip", ".rar", ".7z", ".tar", ".nupkg", ".jar", ".gz", ".tar.gz", ".tgz",
        };
    }
}
