// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

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

        public static bool IsTextFile(string path)
        {
            try
            {
                using var stream = ReadHelper.OpenReadOnly(path);
                int bytesToRead = (int)Math.Min(SampleSize, stream.Length);
                if (bytesToRead == 0)
                {
                    return true;
                }

                var buffer = new byte[bytesToRead];
                int bytesRead = stream.Read(buffer, 0, bytesToRead);

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
            catch (Exception ex)
            {
                Logger.LogError("Failed to determine if file is text: " + ex.Message);
                return false;
            }
        }
    }
}
