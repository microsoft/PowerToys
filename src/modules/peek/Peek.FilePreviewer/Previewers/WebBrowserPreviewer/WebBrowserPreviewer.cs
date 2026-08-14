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

            // SVG - using WebView2 for better compatibility with complex SVGs
            // (e.g., from Adobe Illustrator, Inkscape)
            ".svg",
        };

        [ObservableProperty]
        private Uri? preview;

        [ObservableProperty]
        private PreviewState state;

        [ObservableProperty]
        private bool isDevFilePreview;

        [ObservableProperty]
        private bool customContextMenu;

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
                    string extension = File.Extension;

                    // Default: non-dev file preview with standard context menu
                    IsDevFilePreview = false;
                    CustomContextMenu = false;

                    // Determine preview strategy based on file type priority
                    if (extension == ".md")
                    {
                        // Markdown files use custom renderer
                        var raw = await ReadHelper.Read(File.Path.ToString());
                        Preview = new Uri(MarkdownHelper.PreviewTempFile(raw, File.Path, TempFolderPath.Path));
                    }
                    else if (extension == ".svg")
                    {
                        // SVG files are rendered directly by WebView2 for better compatibility
                        // with complex SVGs from Adobe Illustrator, Inkscape, etc.
                        Preview = new Uri(File.Path);
                    }
                    else if (extension == ".html" || extension == ".htm")
                    {
                        // Simple html file to preview. Shouldn't do things like enabling scripts or using a virtual mapped directory.
                        Preview = new Uri(File.Path);
                    }
                    else if (extension == ".pdf")
                    {
                        Preview = new Uri(File.Path);
                    }
                    else
                    {
                        // Source code files use Monaco editor. Extensions Monaco doesn't recognize
                        // (including files with no extension, sniffed as text by IsTextFallbackSupported)
                        // fall back to plaintext highlighting.
                        IsDevFilePreview = true;
                        CustomContextMenu = true;
                        var raw = await ReadHelper.Read(File.Path.ToString());
                        Preview = new Uri(MonacoHelper.PreviewTempFile(raw, extension, TempFolderPath.Path, _previewSettings.SourceCodeTryFormat, _previewSettings.SourceCodeWrapText, _previewSettings.SourceCodeStickyScroll, _previewSettings.SourceCodeFontSize, _previewSettings.SourceCodeMinimap));
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

        /// <summary>
        /// Last-resort check for files with no extension, or an extension Peek doesn't otherwise
        /// recognize, whose content is sniffed to be text. Should only be consulted after every
        /// other previewer (including the shell preview handler) has declined the item.
        /// </summary>
        public static bool IsTextFallbackSupported(IFileSystemItem item)
        {
            return item is FileItem && !IsItemSupported(item) && TextFileHelper.IsTextFile(item.Path);
        }

        private bool HasFailedLoadingPreview()
        {
            return !(DisplayInfoTask?.Result ?? true);
        }
    }
}
