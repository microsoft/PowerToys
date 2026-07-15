// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Windows.Storage;
using Windows.Storage.Streams;

namespace ManagedCommon
{
    public static class ClipboardHelper
    {
        private static readonly Lazy<ClipboardService> Service = new(
            () => new ClipboardService(new WindowsClipboardBackend(), new ClipboardThreadExecutor()),
            LazyThreadSafetyMode.ExecutionAndPublication);

        public static bool TrySetText(string? text, bool flush = false)
        {
            return Service.Value.TrySetText(text, flush);
        }

        public static Task<bool> TrySetTextAsync(string? text, bool flush = false)
        {
            return Service.Value.TrySetTextAsync(text, flush);
        }

        public static bool TryGetText(out string? text)
        {
            return Service.Value.TryGetText(out text);
        }

        public static Task<ClipboardReadResult<string>> TryGetTextAsync()
        {
            return Service.Value.TryGetTextAsync();
        }

        public static bool TrySetRtf(string? rtf, bool flush = false)
        {
            return Service.Value.TrySetRtf(rtf, flush);
        }

        public static Task<bool> TrySetRtfAsync(string? rtf, bool flush = false)
        {
            return Service.Value.TrySetRtfAsync(rtf, flush);
        }

        public static bool TryGetRtf(out string? rtf)
        {
            return Service.Value.TryGetRtf(out rtf);
        }

        public static Task<ClipboardReadResult<string>> TryGetRtfAsync()
        {
            return Service.Value.TryGetRtfAsync();
        }

        public static bool TrySetStorageItems(
            IEnumerable<IStorageItem>? items,
            bool flush = false)
        {
            return Service.Value.TrySetStorageItems(items, flush);
        }

        public static Task<bool> TrySetStorageItemsAsync(
            IEnumerable<IStorageItem>? items,
            bool flush = false)
        {
            return Service.Value.TrySetStorageItemsAsync(items, flush);
        }

        public static bool TrySetFilePaths(
            IEnumerable<string>? paths,
            bool flush = false)
        {
            return Service.Value.TrySetFilePaths(paths, flush);
        }

        public static Task<bool> TrySetFilePathsAsync(
            IEnumerable<string>? paths,
            bool flush = false)
        {
            return Service.Value.TrySetFilePathsAsync(paths, flush);
        }

        public static bool TryGetStorageItems(
            out IReadOnlyList<IStorageItem>? items)
        {
            return Service.Value.TryGetStorageItems(out items);
        }

        public static Task<ClipboardReadResult<IReadOnlyList<IStorageItem>>> TryGetStorageItemsAsync()
        {
            return Service.Value.TryGetStorageItemsAsync();
        }

        public static bool TryGetFilePaths(out IReadOnlyList<string>? paths)
        {
            return Service.Value.TryGetFilePaths(out paths);
        }

        public static Task<ClipboardReadResult<IReadOnlyList<string>>> TryGetFilePathsAsync()
        {
            return Service.Value.TryGetFilePathsAsync();
        }

        public static bool TrySetImage(RandomAccessStreamReference? image, bool flush = false)
        {
            return Service.Value.TrySetImage(image, flush);
        }

        public static Task<bool> TrySetImageAsync(
            RandomAccessStreamReference? image,
            bool flush = false)
        {
            return Service.Value.TrySetImageAsync(image, flush);
        }

        public static bool TrySetImage(Stream? encodedImage, bool flush = false)
        {
            return Service.Value.TrySetImage(encodedImage, flush);
        }

        public static Task<bool> TrySetImageAsync(Stream? encodedImage, bool flush = false)
        {
            return Service.Value.TrySetImageAsync(encodedImage, flush);
        }

        public static bool TryGetImage(out RandomAccessStreamReference? image)
        {
            return Service.Value.TryGetImage(out image);
        }

        public static Task<ClipboardReadResult<RandomAccessStreamReference>> TryGetImageAsync()
        {
            return Service.Value.TryGetImageAsync();
        }

        public static bool TryGetImageStream(out Stream? encodedImage)
        {
            return Service.Value.TryGetImageStream(out encodedImage);
        }

        public static Task<ClipboardReadResult<Stream>> TryGetImageStreamAsync()
        {
            return Service.Value.TryGetImageStreamAsync();
        }
    }
}
