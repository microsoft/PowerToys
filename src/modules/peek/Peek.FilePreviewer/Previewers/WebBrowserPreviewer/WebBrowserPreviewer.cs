// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Peek.Common.Constants;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Windows.Foundation;

namespace Peek.FilePreviewer.Previewers
{
    public partial class WebBrowserPreviewer : ObservableObject, IBrowserPreviewer, IDisposable
    {
        private static readonly HashSet<string> _supportedFileTypes = new HashSet<string>
        {
            // Web
            ".html",
            ".htm",

            // Document
            ".pdf",

            // Markdown
            ".md",
        };

        private static readonly HashSet<string> _supportedMonacoFileTypes = MonacoHelper.GetExtensions();

        [ObservableProperty]
        private Uri? preview;

        [ObservableProperty]
        private PreviewState state;

        [ObservableProperty]
        private bool isDevFilePreview;

        public WebBrowserPreviewer(IFileSystemItem file)
        {
            File = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private IFileSystemItem File { get; }

        public bool IsPreviewLoaded => preview != null;

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? DisplayInfoTask { get; set; }

        public Task<Size?> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            Size? size = null;
            return Task.FromResult(size);
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            State = PreviewState.Loading;
            await LoadDisplayInfoAsync(cancellationToken);

            if (HasFailedLoadingPreview())
            {
                State = PreviewState.Error;
            }
        }

        public Task<bool> LoadDisplayInfoAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Dispatcher.RunOnUiThread(async () =>
                {
                    bool isHtml = File.Extension == ".html";
                    bool isMarkdown = File.Extension == ".md";
                    IsDevFilePreview = _supportedMonacoFileTypes.Contains(File.Extension);

                    if (IsDevFilePreview && !isHtml && !isMarkdown)
                    {
                        var raw = await ReadHelper.Read(File.Path.ToString());
                        Preview = new Uri(MonacoHelper.PreviewTempFile(raw, File.Extension, TempFolderPath.Path));
                    }
                    else if (isMarkdown)
                    {
                        var raw = await ReadHelper.Read(File.Path.ToString());
                        Preview = new Uri(MarkdownHelper.PreviewTempFile(raw, File.Path, TempFolderPath.Path));
                    }
                    else
                    {
                        Preview = new Uri(File.Path);
                    }
                });
            });
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageItem = await File.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
        }

        public static bool IsFileTypeSupported(string fileExt)
        {
            return _supportedFileTypes.Contains(fileExt) || _supportedMonacoFileTypes.Contains(fileExt);
        }

        private bool HasFailedLoadingPreview()
        {
            return !(DisplayInfoTask?.Result ?? true);
        }
    }
}
