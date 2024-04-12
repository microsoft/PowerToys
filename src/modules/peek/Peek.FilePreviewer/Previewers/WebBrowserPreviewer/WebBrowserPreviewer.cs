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
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Interfaces;

namespace Peek.FilePreviewer.Previewers
{
    public partial class WebBrowserPreviewer : ObservableObject, IBrowserPreviewer, IDisposable
    {
        private readonly IPreviewSettings _previewSettings;

        private static readonly HashSet<string> _supportedFileTypes = new()
        {
            // Web
            ".html",
            ".htm",

            // Document
            ".pdf",

            // Markdown
            ".md",
        };

        [ObservableProperty]
        private Uri? preview;

        [ObservableProperty]
        private PreviewState state;

        [ObservableProperty]
        private bool isDevFilePreview;

        private bool disposed;

        public WebBrowserPreviewer(IFileSystemItem file, IPreviewSettings previewSettings)
        {
            _previewSettings = previewSettings;
            File = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        ~WebBrowserPreviewer()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual async void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                await Microsoft.PowerToys.FilePreviewCommon.Helper.CleanupTempDirAsync(TempFolderPath.Path);
                disposed = true;
            }
        }

        private IFileSystemItem File { get; }

        public bool IsPreviewLoaded => Preview != null;

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? DisplayInfoTask { get; set; }

        public Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new PreviewSize { MonitorSize = null });
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
                    bool isHtml = File.Extension == ".html" || File.Extension == ".htm";
                    bool isMarkdown = File.Extension == ".md";
                    IsDevFilePreview = MonacoHelper.SupportedMonacoFileTypes.Contains(File.Extension);

                    if (IsDevFilePreview && !isHtml && !isMarkdown)
                    {
                        var raw = await ReadHelper.Read(File.Path.ToString());
                        Preview = new Uri(MonacoHelper.PreviewTempFile(raw, File.Extension, TempFolderPath.Path, _previewSettings.SourceCodeTryFormat, _previewSettings.SourceCodeWrapText, _previewSettings.SourceCodeStickyScroll, _previewSettings.SourceCodeFontSize));
                    }
                    else if (isMarkdown)
                    {
                        var raw = await ReadHelper.Read(File.Path.ToString());
                        Preview = new Uri(MarkdownHelper.PreviewTempFile(raw, File.Path, TempFolderPath.Path));
                    }
                    else
                    {
                        // Simple html file to preview. Shouldn't do things like enabling scripts or using a virtual mapped directory.
                        IsDevFilePreview = false;
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

        public static bool IsItemSupported(IFileSystemItem item)
        {
            return _supportedFileTypes.Contains(item.Extension) || MonacoHelper.SupportedMonacoFileTypes.Contains(item.Extension);
        }

        private bool HasFailedLoadingPreview()
        {
            return !(DisplayInfoTask?.Result ?? true);
        }
    }
}
