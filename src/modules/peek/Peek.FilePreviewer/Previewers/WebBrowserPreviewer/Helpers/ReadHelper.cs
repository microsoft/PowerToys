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

        // Prefix size fed to the charset detector. Sampling keeps detection (and its scan) bounded
        // regardless of file size; a file that is plain ASCII for its first CharsetSampleSizeBytes but
        // carries non-ASCII content only later could be mis-detected, a trade-off that matches how
        // editors and tools like git sniff encoding.
        private const int CharsetSampleSizeBytes = 64 * 1024;

        public static async Task<string> Read(string path, long maxReadableFileSizeBytes = MaxReadableFileSizeBytes, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var fs = OpenReadOnly(path);
            if (fs.Length > maxReadableFileSizeBytes)
            {
                throw new InvalidOperationException($"File '{path}' exceeds the maximum previewable size of {maxReadableFileSizeBytes} bytes.");
            }

            // Detect the charset from a bounded prefix of the same file stream handle.
            int sampleSize = (int)Math.Min(fs.Length, CharsetSampleSizeBytes);
            var sample = new byte[sampleSize];
            int sampleRead = await fs.ReadAtLeastAsync(sample, sampleSize, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            DetectionResult result = CharsetDetector.DetectFromBytes(sampleRead == sample.Length ? sample : sample[..sampleRead]);

            // Check if the detected encoding is not null; otherwise, default to UTF-8
            Encoding encodingToUse = result.Detected?.Encoding ?? Encoding.UTF8;

            // Rewind and stream the decode so the whole file is never held in memory at once.
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
