// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Helpers;
using Windows.Foundation;

namespace Peek.FilePreviewer.Previewers;

public partial class SpecialFolderPreviewer : ObservableObject, ISpecialFolderPreviewer, IDisposable
{
    private readonly DispatcherTimer _syncDetailsDispatcherTimer = new();
    private ulong _folderSize;
    private DateTime? _dateModified;

    [ObservableProperty]
    private SpecialFolderPreviewData preview = new();

    [ObservableProperty]
    private PreviewState state;

    public SpecialFolderPreviewer(IFileSystemItem file)
    {
        _syncDetailsDispatcherTimer.Interval = TimeSpan.FromMilliseconds(500);
        _syncDetailsDispatcherTimer.Tick += DetailsDispatcherTimer_Tick;

        Item = file;
        Preview.FileName = file.Name;
        Dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    public static bool IsItemSupported(IFileSystemItem item)
    {
        // Always allow know special folders.
        bool isKnownSpecialFolder = KnownSpecialFolders.FoldersByParsingName.ContainsKey(item.ParsingName);

        // Allow empty paths unless Unc; icons don't load correctly for Unc paths.
        bool isEmptyNonUncPath = string.IsNullOrEmpty(item.Path) && !PathHelper.IsUncPath(item.ParsingName);

        return isKnownSpecialFolder || isEmptyNonUncPath;
    }

    private IFileSystemItem Item { get; }

    private DispatcherQueue Dispatcher { get; }

    public void Dispose()
    {
        _syncDetailsDispatcherTimer.Tick -= DetailsDispatcherTimer_Tick;
        GC.SuppressFinalize(this);
    }

    public Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
    {
        Size? size = new(680, 500);
        var previewSize = new PreviewSize { MonitorSize = size, UseEffectivePixels = true };
        return Task.FromResult(previewSize);
    }

    public async Task LoadPreviewAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        State = PreviewState.Loading;

        var tasks = await Task.WhenAll(LoadIconPreviewAsync(cancellationToken), LoadDisplayInfoAsync(cancellationToken));

        State = tasks.All(task => task) ? PreviewState.Loaded : PreviewState.Error;
    }

    public async Task CopyAsync()
    {
        await Dispatcher.RunOnUiThread(async () =>
        {
            var storageItem = await Item.GetStorageItemAsync();
            ClipboardHelper.SaveToClipboard(storageItem);
        });
    }

    public async Task<bool> LoadIconPreviewAsync(CancellationToken cancellationToken)
    {
        bool isIconValid = false;

        var isTaskSuccessful = await TaskExtension.RunSafe(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Dispatcher.RunOnUiThread(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var iconBitmap = await IconHelper.GetIconAsync(Item.ParsingName, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                isIconValid = iconBitmap != null;
                Preview.IconPreview = iconBitmap ?? new SvgImageSource(new Uri("ms-appx:///Assets/Peek/DefaultFileIcon.svg"));
            });
        });

        return isIconValid && isTaskSuccessful;
    }

    public async Task<bool> LoadDisplayInfoAsync(CancellationToken cancellationToken)
    {
        bool isDisplayValid = false;

        var isTaskSuccessful = await TaskExtension.RunSafe(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileType = await Task.Run(Item.GetContentTypeAsync);

            cancellationToken.ThrowIfCancellationRequested();

            isDisplayValid = fileType != null;

            await Dispatcher.RunOnUiThread(() =>
            {
                Preview.FileType = fileType;
                return Task.CompletedTask;
            });

            RunUpdateDetailsWorkflow(cancellationToken);
        });

        return isDisplayValid && isTaskSuccessful;
    }

    private void RunUpdateDetailsWorkflow(CancellationToken cancellationToken)
    {
        Task.Run(
            async () =>
            {
                try
                {
                    await Dispatcher.RunOnUiThread(_syncDetailsDispatcherTimer.Start);
                    ComputeDetails(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to update special folder details", ex);
                }
                finally
                {
                    await Dispatcher.RunOnUiThread(_syncDetailsDispatcherTimer.Stop);
                }

                await Dispatcher.RunOnUiThread(SyncDetails);
            },
            cancellationToken);
    }

    private void ComputeDetails(CancellationToken cancellationToken)
    {
        _dateModified = Item.DateModified;

        switch (KnownSpecialFolders.FoldersByParsingName.GetValueOrDefault(Item.ParsingName, KnownSpecialFolder.None))
        {
            case KnownSpecialFolder.None:
                break;

            case KnownSpecialFolder.RecycleBin:
                ThreadHelper.RunOnSTAThread(() => { ComputeRecycleBinDetails(cancellationToken); });
                cancellationToken.ThrowIfCancellationRequested();
                break;
        }
    }

    private void ComputeRecycleBinDetails(CancellationToken cancellationToken)
    {
        var shell = new Shell32.Shell();
        var recycleBin = shell.NameSpace(10); // CSIDL_BITBUCKET

        foreach (dynamic item in recycleBin.Items())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _folderSize += Convert.ToUInt64(item.Size);
        }
    }

    private void SyncDetails()
    {
        Preview.FileSize = _folderSize == 0 ? string.Empty : ReadableStringHelper.BytesToReadableString(_folderSize);
        Preview.DateModified = _dateModified?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
    }

    private void DetailsDispatcherTimer_Tick(object? sender, object e)
    {
        SyncDetails();
    }
}
