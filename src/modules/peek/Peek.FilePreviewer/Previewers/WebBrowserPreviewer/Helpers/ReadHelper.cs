// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using UtfUnknown;

namespace Peek.FilePreviewer.Previewers
{
    public static class ReadHelper
    {
        // Fallback cap used when the caller doesn't pass an explicit limit. Content read here is later
        // base64-encoded, embedded in a temp HTML file, and decoded by WebView2, so an unbounded read
        // can cause a large memory spike. The user-configurable limit lives in Peek preview settings.
        public const long MaxReadableFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        public static async Task<string> Read(string path, long maxReadableFileSizeBytes = MaxReadableFileSizeBytes, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var fs = OpenReadOnly(path);
            if (fs.Length > maxReadableFileSizeBytes)
            {
                throw new InvalidOperationException($"File '{path}' exceeds the maximum previewable size of {maxReadableFileSizeBytes} bytes.");
            }

            // Detect the charset from the whole file (bounded by maxReadableFileSizeBytes) using the same stream handle.
            // Detecting from only a prefix can commit to the wrong decoder, e.g. us-ascii when the non-ASCII content
            // starts later, or iso-8859-1 when the prefix ends partway through a multi-byte UTF-8 character.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            DetectionResult result = await CharsetDetector.DetectFromStreamAsync(fs, maxReadableFileSizeBytes, cancellationToken).ConfigureAwait(false);

            // Check if the detected encoding is not null; otherwise, default to UTF-8
            Encoding encodingToUse = result.Detected?.Encoding ?? Encoding.UTF8;

            // Rewind and decode incrementally so the raw bytes are never buffered in full. The decoded text is still accumulated
            // into a single string, so peak memory scales with the file size, bounded by maxReadableFileSizeBytes.
            // StreamReader strips a byte order mark rather than surface it as a leading U+FEFF character; the per-chunk
            // length check catches a file being appended to that would otherwise grow the preview past the limit.
            fs.Position = 0;
            using var sr = new StreamReader(fs, encodingToUse, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            var buffer = new char[81920];
            var content = new StringBuilder();
            int charsRead;
            while ((charsRead = await sr.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                content.Append(buffer, 0, charsRead);

                if (fs.Position > maxReadableFileSizeBytes)
                {
                    throw new InvalidOperationException($"File '{path}' exceeds the maximum previewable size of {maxReadableFileSizeBytes} bytes.");
                }
            }

            return content.ToString();
        }

        public static FileStream OpenReadOnly(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
    }
}
