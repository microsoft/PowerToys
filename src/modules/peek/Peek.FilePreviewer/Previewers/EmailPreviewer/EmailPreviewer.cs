// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
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

namespace Peek.FilePreviewer.Previewers.EmailPreviewer
{
    public partial class EmailPreviewer : ObservableObject, IBrowserPreviewer, IDisposable
    {
        private static readonly HashSet<string> SupportedFileTypes = new(StringComparer.OrdinalIgnoreCase) { ".eml", ".msg" };
        private readonly IFileSystemItem _file;
        private readonly DispatcherQueue _dispatcher;
        private string? _tempFile;

        [ObservableProperty]
        private Uri? preview;

        [ObservableProperty]
        private PreviewState state;

        public EmailPreviewer(IFileSystemItem file)
        {
            _file = file;
            _dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public bool IsPreviewLoaded => Preview != null;

        public bool IsDevFilePreview => false;

        public bool CustomContextMenu => false;

        public bool AllowExternalImages => true;

        public static bool IsItemSupported(IFileSystemItem item) => SupportedFileTypes.Contains(item.Extension);

        public Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken) => Task.FromResult(new PreviewSize { MonitorSize = null });

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            State = PreviewState.Loading;

            try
            {
                EmailMessage message = await Task.Run(
                    () => _file.Extension.Equals(".eml", StringComparison.OrdinalIgnoreCase) ? EmlReader.Read(_file.Path) : MsgReader.Read(_file.Path),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                Directory.CreateDirectory(TempFolderPath.Path);
                string tempFile = Path.Combine(TempFolderPath.Path, $"{Guid.NewGuid():N}.html");
                await File.WriteAllTextAsync(tempFile, EmailHtmlRenderer.Render(message), cancellationToken);
                _tempFile = tempFile;

                await _dispatcher.RunOnUiThread(() => Preview = new Uri(tempFile));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                State = PreviewState.Error;
            }
        }

        public async Task CopyAsync()
        {
            await _dispatcher.RunOnUiThread(async () =>
            {
                var storageItem = await _file.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
        }

        public void Dispose()
        {
            if (_tempFile != null)
            {
                try
                {
                    File.Delete(_tempFile);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                _tempFile = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
