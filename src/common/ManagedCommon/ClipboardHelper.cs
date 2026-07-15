// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

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
    }
}
