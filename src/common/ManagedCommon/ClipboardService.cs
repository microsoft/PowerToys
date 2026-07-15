// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Windows.ApplicationModel.DataTransfer;

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

        private Task<bool> TrySetPackageAsync(
            Func<ClipboardWritePackage> packageFactory,
            bool flush)
        {
            return _executor.InvokeAsync(() =>
            {
                for (int attempt = 1; attempt <= _maxAttempts; attempt++)
                {
                    using ClipboardWritePackage package = packageFactory();

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
    }
}
