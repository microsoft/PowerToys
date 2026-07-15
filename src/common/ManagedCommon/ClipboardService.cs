// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ManagedCommon
{
    internal sealed class ClipboardService
    {
        private readonly IClipboardBackend _backend;
        private readonly ClipboardThreadExecutor _executor;
        private readonly int _maxAttempts;
        private readonly int _retryDelayMilliseconds;

        internal ClipboardService(
            IClipboardBackend backend,
            ClipboardThreadExecutor executor,
            int maxAttempts = 10,
            int retryDelayMilliseconds = 10)
        {
            ArgumentNullException.ThrowIfNull(backend);
            ArgumentNullException.ThrowIfNull(executor);

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);
            ArgumentOutOfRangeException.ThrowIfNegative(retryDelayMilliseconds);

            _backend = backend;
            _executor = executor;
            _maxAttempts = maxAttempts;
            _retryDelayMilliseconds = retryDelayMilliseconds;
        }

        internal bool TrySetText(string? text, bool flush)
        {
            return TrySetTextAsync(text, flush).GetAwaiter().GetResult();
        }

        internal Task<bool> TrySetTextAsync(string? text, bool flush)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Task.FromResult(false);
            }

            return TrySetPackageAsync(
                () =>
                {
                    var package = new DataPackage();
                    package.SetText(text);
                    return new ClipboardWritePackage(package);
                },
                flush);
        }

        internal bool TryGetText(out string? text)
        {
            ClipboardReadResult<string> result = TryGetTextAsync().GetAwaiter().GetResult();
            text = result.Value;
            return result.Succeeded;
        }

        internal Task<ClipboardReadResult<string>> TryGetTextAsync()
        {
            return TryGetAsync(
                StandardDataFormats.Text,
                view => view.GetTextAsync().AsTask());
        }

        internal bool TrySetRtf(string? rtf, bool flush)
        {
            return TrySetRtfAsync(rtf, flush).GetAwaiter().GetResult();
        }

        internal Task<bool> TrySetRtfAsync(string? rtf, bool flush)
        {
            if (string.IsNullOrEmpty(rtf))
            {
                return Task.FromResult(false);
            }

            return TrySetPackageAsync(
                () =>
                {
                    var package = new DataPackage();
                    package.SetRtf(rtf);
                    return new ClipboardWritePackage(package);
                },
                flush);
        }

        internal bool TryGetRtf(out string? rtf)
        {
            ClipboardReadResult<string> result = TryGetRtfAsync().GetAwaiter().GetResult();
            rtf = result.Value;
            return result.Succeeded;
        }

        internal Task<ClipboardReadResult<string>> TryGetRtfAsync()
        {
            return TryGetAsync(
                StandardDataFormats.Rtf,
                view => view.GetRtfAsync().AsTask());
        }

        internal bool TrySetImage(RandomAccessStreamReference? image, bool flush)
        {
            return TrySetImageAsync(image, flush).GetAwaiter().GetResult();
        }

        internal Task<bool> TrySetImageAsync(RandomAccessStreamReference? image, bool flush)
        {
            if (image is null)
            {
                return Task.FromResult(false);
            }

            return TrySetPackageAsync(
                () =>
                {
                    var package = new DataPackage();
                    package.SetBitmap(image);
                    return new ClipboardWritePackage(package);
                },
                flush);
        }

        internal bool TrySetImage(Stream? encodedImage, bool flush)
        {
            return TrySetImageAsync(encodedImage, flush).GetAwaiter().GetResult();
        }

        internal async Task<bool> TrySetImageAsync(Stream? encodedImage, bool flush)
        {
            if (encodedImage is null)
            {
                return false;
            }

            byte[] bytes;
            using (var copy = new MemoryStream())
            {
                await encodedImage.CopyToAsync(copy).ConfigureAwait(false);
                if (copy.Length == 0)
                {
                    return false;
                }

                bytes = copy.ToArray();
            }

            return await TrySetPackageAsync(() => CreateImagePackageAsync(bytes), flush).ConfigureAwait(false);
        }

        internal bool TryGetImage(out RandomAccessStreamReference? image)
        {
            ClipboardReadResult<RandomAccessStreamReference> result =
                TryGetImageAsync().GetAwaiter().GetResult();
            image = result.Value;
            return result.Succeeded;
        }

        internal Task<ClipboardReadResult<RandomAccessStreamReference>> TryGetImageAsync()
        {
            return TryGetAsync(
                StandardDataFormats.Bitmap,
                view => view.GetBitmapAsync().AsTask());
        }

        internal bool TryGetImageStream(out Stream? encodedImage)
        {
            ClipboardReadResult<Stream> result =
                TryGetImageStreamAsync().GetAwaiter().GetResult();
            encodedImage = result.Value;
            return result.Succeeded;
        }

        internal Task<ClipboardReadResult<Stream>> TryGetImageStreamAsync()
        {
            return TryGetAsync<Stream>(
                StandardDataFormats.Bitmap,
                async view =>
                {
                    RandomAccessStreamReference reference = await view.GetBitmapAsync().AsTask();
                    using IRandomAccessStreamWithContentType source = await reference.OpenReadAsync().AsTask();
                    using Stream sourceStream = source.AsStreamForRead();
                    var output = new MemoryStream();

                    try
                    {
                        await sourceStream.CopyToAsync(output);
                        output.Position = 0;
                        return output;
                    }
                    catch
                    {
                        output.Dispose();
                        throw;
                    }
                });
        }

        internal bool TrySetStorageItems(
            IEnumerable<IStorageItem>? items,
            bool flush)
        {
            return TrySetStorageItemsAsync(items, flush).GetAwaiter().GetResult();
        }

        internal Task<bool> TrySetStorageItemsAsync(
            IEnumerable<IStorageItem>? items,
            bool flush)
        {
            if (items is null)
            {
                return Task.FromResult(false);
            }

            IStorageItem[] materializedItems = items.ToArray();
            if (materializedItems.Length == 0 || materializedItems.Any(item => item is null))
            {
                return Task.FromResult(false);
            }

            return TrySetPackageAsync(
                () =>
                {
                    var package = new DataPackage();
                    package.SetStorageItems(materializedItems);
                    return new ClipboardWritePackage(package);
                },
                flush);
        }

        internal bool TrySetFilePaths(IEnumerable<string>? paths, bool flush)
        {
            return TrySetFilePathsAsync(paths, flush).GetAwaiter().GetResult();
        }

        internal async Task<bool> TrySetFilePathsAsync(
            IEnumerable<string>? paths,
            bool flush)
        {
            if (paths is null)
            {
                return false;
            }

            string[] materializedPaths = paths.ToArray();
            if (materializedPaths.Length == 0 || materializedPaths.Any(string.IsNullOrWhiteSpace))
            {
                return false;
            }

            var storageItems = new List<IStorageItem>(materializedPaths.Length);
            foreach (string path in materializedPaths)
            {
                string fullPath = Path.GetFullPath(path);
                System.IO.FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(fullPath);
                }
                catch (FileNotFoundException)
                {
                    throw CreateMissingPathException(fullPath);
                }
                catch (DirectoryNotFoundException)
                {
                    throw CreateMissingPathException(fullPath);
                }

                IStorageItem storageItem =
                    (attributes & System.IO.FileAttributes.Directory) != 0
                        ? await StorageFolder.GetFolderFromPathAsync(fullPath).AsTask().ConfigureAwait(false)
                        : await StorageFile.GetFileFromPathAsync(fullPath).AsTask().ConfigureAwait(false);
                storageItems.Add(storageItem);
            }

            return await TrySetStorageItemsAsync(storageItems, flush).ConfigureAwait(false);
        }

        internal bool TryGetStorageItems(
            out IReadOnlyList<IStorageItem>? items)
        {
            ClipboardReadResult<IReadOnlyList<IStorageItem>> result =
                TryGetStorageItemsAsync().GetAwaiter().GetResult();
            items = result.Value;
            return result.Succeeded;
        }

        internal Task<ClipboardReadResult<IReadOnlyList<IStorageItem>>> TryGetStorageItemsAsync()
        {
            return TryGetAsync<IReadOnlyList<IStorageItem>>(
                StandardDataFormats.StorageItems,
                async view => (await view.GetStorageItemsAsync().AsTask()).ToArray());
        }

        internal bool TryGetFilePaths(out IReadOnlyList<string>? paths)
        {
            ClipboardReadResult<IReadOnlyList<string>> result =
                TryGetFilePathsAsync().GetAwaiter().GetResult();
            paths = result.Value;
            return result.Succeeded;
        }

        internal async Task<ClipboardReadResult<IReadOnlyList<string>>> TryGetFilePathsAsync()
        {
            ClipboardReadResult<IReadOnlyList<IStorageItem>> storageItems =
                await TryGetStorageItemsAsync().ConfigureAwait(false);
            if (!storageItems.Succeeded || storageItems.Value is null)
            {
                return ClipboardReadResult<IReadOnlyList<string>>.Failure();
            }

            var paths = new List<string>(storageItems.Value.Count);
            foreach (IStorageItem item in storageItems.Value)
            {
                if (string.IsNullOrEmpty(item.Path))
                {
                    return ClipboardReadResult<IReadOnlyList<string>>.Failure();
                }

                paths.Add(item.Path);
            }

            return ClipboardReadResult<IReadOnlyList<string>>.Success(paths);
        }

        private Task<bool> TrySetPackageAsync(
            Func<ClipboardWritePackage> packageFactory,
            bool flush)
        {
            ArgumentNullException.ThrowIfNull(packageFactory);

            return TrySetPackageAsync(
                () => Task.FromResult(packageFactory()),
                flush);
        }

        private Task<bool> TrySetPackageAsync(
            Func<Task<ClipboardWritePackage>> packageFactoryAsync,
            bool flush)
        {
            ArgumentNullException.ThrowIfNull(packageFactoryAsync);

            return _executor.InvokeAsync(async () =>
            {
                for (int attempt = 1; attempt <= _maxAttempts; attempt++)
                {
                    using ClipboardWritePackage package = await packageFactoryAsync();

                    try
                    {
                        _backend.SetContent(package.Content);
                        package.TransferOwnership();

                        if (flush)
                        {
                            _backend.Flush();
                        }

                        return true;
                    }
                    catch (Exception ex) when (IsTransientClipboardException(ex))
                    {
                        if (attempt == _maxAttempts)
                        {
                            return false;
                        }

                        Thread.Sleep(_retryDelayMilliseconds);
                    }
                }

                return false;
            });
        }

        private Task<ClipboardReadResult<T>> TryGetAsync<T>(
            string format,
            Func<DataPackageView, Task<T>> getValueAsync)
            where T : class
        {
            ArgumentNullException.ThrowIfNull(getValueAsync);

            return _executor.InvokeAsync(async () =>
            {
                for (int attempt = 1; attempt <= _maxAttempts; attempt++)
                {
                    try
                    {
                        DataPackageView view = _backend.GetContent();
                        if (!view.Contains(format))
                        {
                            return ClipboardReadResult<T>.Failure();
                        }

                        T value = await getValueAsync(view);
                        return ClipboardReadResult<T>.Success(value);
                    }
                    catch (Exception ex) when (IsTransientClipboardException(ex))
                    {
                        if (attempt == _maxAttempts)
                        {
                            return ClipboardReadResult<T>.Failure();
                        }

                        Thread.Sleep(_retryDelayMilliseconds);
                    }
                }

                return ClipboardReadResult<T>.Failure();
            });
        }

        private static bool IsTransientClipboardException(Exception exception)
        {
            return exception is COMException or UnauthorizedAccessException;
        }

        private static FileNotFoundException CreateMissingPathException(string fullPath)
        {
            return new FileNotFoundException(
                $"Clipboard path does not exist: {fullPath}",
                fullPath);
        }

        private static async Task<ClipboardWritePackage> CreateImagePackageAsync(byte[] bytes)
        {
            var stream = new InMemoryRandomAccessStream();

            try
            {
                using (var writer = new DataWriter(stream))
                {
                    writer.WriteBytes(bytes);
                    await writer.StoreAsync().AsTask();
                    _ = writer.DetachStream();
                }

                stream.Seek(0);
                var package = new DataPackage();
                package.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
                return new ClipboardWritePackage(package, stream);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }
    }
}
