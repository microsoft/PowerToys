// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Windows.Foundation;

namespace Peek.FilePreviewer.Previewers
{
    public partial class TextFilePreviewer : ObservableObject, ITextFilePreviewer, IDisposable
    {
        [ObservableProperty]
        private Uri? preview;

        [ObservableProperty]
        private PreviewState state;

        [ObservableProperty]
        private string tempDataFolder = Environment.GetEnvironmentVariable("USERPROFILE") +
                        "\\AppData\\LocalLow\\Microsoft\\PowerToys\\Peek-Temp";

        public TextFilePreviewer(IFileSystemItem file)
        {
            Item = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public bool IsPreviewLoaded => preview != null;

        private IFileSystemItem Item { get; }

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? DisplayInfoTask { get; set; }

        public void Dispose()
        {
            Microsoft.PowerToys.FilePreviewCommon.Helper.CleanupTempDir(tempDataFolder);
            GC.SuppressFinalize(this);
        }

        public static bool IsFileTypeSupported(string fileExt)
        {
            return _supportedFileTypes.Contains(fileExt);
        }

        public Task<Size?> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            Size? size = new Size(1640, 1460);
            return Task.FromResult(size);
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            State = PreviewState.Loading;

            DisplayInfoTask = LoadDisplayInfoAsync(cancellationToken);

            await Task.WhenAll(DisplayInfoTask);

            if (HasFailedLoadingPreview())
            {
                State = PreviewState.Error;
            }
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageItem = await Item.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
        }

        public Task<bool> LoadDisplayInfoAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Dispatcher.RunOnUiThread(async () =>
                {
                    var raw = await ReadHelper.Read(Item.Path.ToString());
                    Preview = new Uri(WebViewHelper.Preview(raw, tempDataFolder));
                });
            });
        }

        partial void OnPreviewChanged(Uri? value)
        {
            if (Preview != null)
            {
                State = PreviewState.Loaded;
            }
        }

        private bool HasFailedLoadingPreview()
        {
            var hasFailedLoadingDisplayInfo = !(DisplayInfoTask?.Result ?? true);

            return hasFailedLoadingDisplayInfo;
        }

        private static readonly HashSet<string> _supportedFileTypes = new HashSet<string>
        {
            ".txt",
            ".md",
        };
    }
}
