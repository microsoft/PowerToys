// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;

namespace Peek.FilePreviewer.Previewers
{
    /// <summary>
    /// Heuristically detects whether a file's content is text, so files with no extension
    /// or an extension Peek doesn't otherwise recognize can still fall back to a plain text
    /// preview instead of just showing file details.
    /// </summary>
    public static class TextFileHelper
    {
        // Matches the sample size commonly used by tools like git to decide whether a file is text or binary.
        private const int SampleSize = 8000;

        // Files larger than this are rejected outright, so an oversized file never reaches ReadHelper.Read
        // and the Monaco/WebView pipeline, which load the full content into memory. See ReadHelper.MaxReadableFileSizeBytes,
        // which enforces the same limit while actually reading a file's content.
        private const long MaxFileSizeBytes = ReadHelper.MaxReadableFileSizeBytes;

        /// <summary>
        /// Determines whether the file at the given path is likely to be a text file, based on its size and content.
        /// </summary>
        /// <param name="path">The path to the file to check.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>True if the file is likely to be a text file; otherwise, false.</returns>
        public static async Task<bool> IsTextFileAsync(string path, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var stream = ReadHelper.OpenReadOnly(path);
                if (stream.Length > MaxFileSizeBytes)
                {
                    return false;
                }

                int bytesToRead = (int)Math.Min(SampleSize, stream.Length);
                if (bytesToRead == 0)
                {
                    return true;
                }

                var buffer = new byte[bytesToRead];
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken);

                // If the file starts with a Unicode BOM, we can assume it's a text file.
                if (HasUnicodeBom(buffer, bytesRead))
                {
                    return true;
                }

                // A NUL byte in the sample is a strong signal of binary content.
                for (int i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] == 0)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                // Let navigation cancellation propagate instead of being reported as "not text".
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to determine if file is text: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Determines whether the given buffer contains a Unicode BOM (Byte Order Mark) for UTF-16 or UTF-32.
        /// </summary>
        /// <param name="buffer">The byte array to check for a BOM.</param>
        /// <param name="bytesRead">The number of bytes read into the buffer.</param>
        /// <returns>True if the buffer contains a Unicode BOM; otherwise, false.</returns>
        private static bool HasUnicodeBom(byte[] buffer, int bytesRead)
        {
            // UTF-32 BOMs must be checked before UTF-16, since the UTF-32LE BOM (FF FE 00 00) starts with the UTF-16LE BOM (FF FE).
            bool isUtf32 = bytesRead >= 4 &&
                ((buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00) ||
                 (buffer[0] == 0x00 && buffer[1] == 0x00 && buffer[2] == 0xFE && buffer[3] == 0xFF));
            if (isUtf32)
            {
                return true;
            }

            bool isUtf16 = bytesRead >= 2 &&
                ((buffer[0] == 0xFF && buffer[1] == 0xFE) ||
                 (buffer[0] == 0xFE && buffer[1] == 0xFF));
            return isUtf16;
        }
    }
}
