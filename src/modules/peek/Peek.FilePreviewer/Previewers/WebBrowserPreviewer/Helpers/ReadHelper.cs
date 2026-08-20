// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using UtfUnknown;

namespace Peek.FilePreviewer.Previewers
{
    public static class ReadHelper
    {
        // Content read here is later base64-encoded, embedded in a temp HTML file, and decoded by
        // WebView2, so an unbounded read can cause a large memory spike.
        public const long MaxReadableFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        public static async Task<string> Read(string path)
        {
            using var fs = OpenReadOnly(path);
            if (fs.Length > MaxReadableFileSizeBytes)
            {
                throw new InvalidOperationException($"File '{path}' exceeds the maximum previewable size of {MaxReadableFileSizeBytes} bytes.");
            }

            DetectionResult result = CharsetDetector.DetectFromFile(path);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Check if the detected encoding is not null; otherwise, default to UTF-8
            Encoding encodingToUse = result.Detected?.Encoding ?? Encoding.UTF8;

            using var sr = new StreamReader(fs, encodingToUse);

            // Read incrementally rather than all at once, so a file that grows past the limit while
            // being read (e.g. an actively written log) is still caught instead of only relying on
            // the length check above.
            var buffer = new char[81920];
            var content = new StringBuilder();
            int charsRead;
            while ((charsRead = await sr.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                content.Append(buffer, 0, charsRead);

                if (fs.Position > MaxReadableFileSizeBytes)
                {
                    throw new InvalidOperationException($"File '{path}' exceeds the maximum previewable size of {MaxReadableFileSizeBytes} bytes.");
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
