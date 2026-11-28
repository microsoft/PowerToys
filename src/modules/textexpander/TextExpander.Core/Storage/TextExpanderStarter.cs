// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;

#nullable enable

namespace Microsoft.PowerToys.TextExpander.Storage
{
    /// <summary>
    /// The starter snippets file, which the editor and the engine write in the default location when it has none.
    /// </summary>
    public static class TextExpanderStarter
    {
        private static readonly (string Trigger, string Replacement)[] Snippets =
        [
            ("!ddate", "{{date}}"),
            ("!ttime", "{{time}}"),
            ("!ddt", "{{datetime}}"),
            ("!isodate", "{{isodate}}"),
            ("!mysig", "Best regards,\nYour name"),
            ("!myemail", "you@example.com"),
        ];

        private static readonly byte[] StarterFileBytes = CreateStarterFileBytes();

        /// <summary>Gets the exact text written for a new snippets file, in UTF-8 without a byte order mark.</summary>
        public static string Content { get; } = Encoding.UTF8.GetString(StarterFileBytes);

        /// <summary>
        /// Writes the starter to <paramref name="path"/> unless a file is already there. The complete file is written
        /// beside it and moved into place, so the editor, the engine or another app creating it at the same time wins
        /// without being overwritten, and nothing reads it half-written. The folder must exist: only the default location
        /// gets the starter, and its callers create that folder first.
        /// </summary>
        /// <returns><see langword="true"/> when this call wrote the file.</returns>
        public static bool TryCreateFile(string path)
            => TryCreateFile(path, beforeMove: null, beforeStagingCleanup: null, beforeStagingCleanupDisposition: null).Created;

        internal static FileResult TryCreateFile(
            string path,
            Action? beforeMove,
            Action<string>? beforeStagingCleanup,
            Action<string>? beforeStagingCleanupDisposition)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            if (File.Exists(path))
            {
                return new FileResult(Created: false);
            }

            string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            bool created = false;
            HandleBoundFileDeletionResult? stagingCleanup = null;
            try
            {
                File.WriteAllBytes(temporaryPath, StarterFileBytes);
                beforeMove?.Invoke();

                try
                {
                    File.Move(temporaryPath, path, overwrite: false);
                    created = true;
                }
                catch (IOException) when (File.Exists(path))
                {
                }
            }
            finally
            {
                beforeStagingCleanup?.Invoke(temporaryPath);
                Action? beforeDisposition = beforeStagingCleanupDisposition is null
                    ? null
                    : () => beforeStagingCleanupDisposition(temporaryPath);
                stagingCleanup = HandleBoundFileDeletion.TryDelete(
                    temporaryPath,
                    StarterFileBytes,
                    beforeDisposition);
                if (!stagingCleanup.Value.Succeeded)
                {
                    Trace.TraceWarning(
                        $"Text Expander starter staging cleanup was incomplete ({stagingCleanup.Value.Status}) for '{stagingCleanup.Value.Path}': {stagingCleanup.Value.Error?.Message}");
                }
            }

            return new FileResult(created)
            {
                StagingCleanup = stagingCleanup,
            };
        }

        private static byte[] CreateStarterFileBytes()
        {
            var file = new SnippetFile();
            foreach ((string trigger, string replacement) in Snippets)
            {
                file.Upsert(null, trigger, replacement);
            }

            return file.ToBytes();
        }

        /// <summary>Whether the starter was written, and how its staging file was cleaned up.</summary>
        public readonly record struct FileResult(bool Created)
        {
            public HandleBoundFileDeletionResult? StagingCleanup { get; init; }
        }
    }
}
