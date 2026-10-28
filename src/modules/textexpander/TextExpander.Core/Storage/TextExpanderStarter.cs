// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

#nullable enable

namespace Microsoft.PowerToys.TextExpander.Storage
{
    /// <summary>
    /// The starter snippet file and safe publication rules shared by the module's editor and engine.
    /// </summary>
    public static class TextExpanderStarter
    {
        private const int FolderClaimTimeoutSeconds = 10;

        private const string RawContent = """
            // ── PowerToys Text Expander ─────────────────────────────
            // Built by Gary Hsu · https://github.com/howdygary
            // Project: https://github.com/howdygary/text-expander
            // Website: https://howdygary.github.io/text-expander/
            //
            // Type a trigger, such as !ddate, and Text Expander replaces it with its
            // snippet. The snippets at the bottom of this file work right away.
            //
            // ── How to write a snippet ──────────────────────────────
            // One line:       trigger=replacement
            // Several lines:  trigger<<<
            //                 first line
            //                 second line
            //                 >>>
            // Lines that start with //, like this one, are notes and are skipped.
            // Changes work as soon as you save this file.
            //
            // ── Variables, filled in each time a snippet expands ────
            // {{date}}             today's date, like 06/01/26
            // {{time}}             the time, like 02:30 PM
            // {{datetime}}         both, like 06/01/26 02:30 PM
            // {{isodate}}          the date year first, like 2026-06-01
            // {{date:FORMAT}}      a date your way: {{date:dddd, MMMM d}} gives Monday, June 1
            //                      (dddd weekday, MMMM month, d day, yyyy year, HH:mm 24-hour time)
            //                      Put words in single quotes, or they're read as codes:
            //                      {{date:'Today is' dddd}} gives Today is Monday
            // {{clipboard}}        the text you copied last, such as a link
            // {{prompt:Question}}  asks you a question and requires an answer,
            //                      like {{prompt:First name}}, then types what you enter
            // {{prompt?:Question}} asks the same way but lets you continue with no answer
            // $|$                  where the cursor ends up: Dear $|$, leaves it after "Dear "
            // \n and \t            a new line and a tab
            //
            // ── Tips ────────────────────────────────────────────────
            // - Start triggers with ! so they don't fire in the middle of a word.
            // - A trigger fires as soon as you type it, so one trigger can't be the start
            //   of another: !sig would fire before you could finish typing !signature.
            // - To sync snippets across devices, choose a folder managed by OneDrive or
            //   another sync service in PowerToys Settings > Text Expander.
            //
            // ── Examples: remove the // and the space after it to use one ──
            // !hello=Hi {{prompt:First name}}, thanks for reaching out!
            // !mdlink=[{{prompt:Link text}}]({{clipboard}})
            // !longdate={{date:dddd, MMMM d, yyyy}}
            // !time24={{date:HH:mm}}
            // !todo=// TODO: $|$ ({{isodate}})
            // !myaddr=123 Example Street\nSpringfield, ZZ 12345
            // !tabhead=Name\tEmail\tTeam
            // For !meeting, do that on all five of its lines:
            // !meeting<<<
            // Meeting notes · {{date:dddd, MMMM d, yyyy}}
            // Attendees: $|$
            // Next steps:
            // >>>
            //
            // ── Your snippets ───────────────────────────────────────

            !ddate={{date}}
            !ttime={{time}}
            !ddt={{datetime}}
            !isodate={{isodate}}
            !mysig=Best regards,\nYour Name
            !myemail=you@example.com

            """;

        /// <summary>The exact text written for a new snippet file.</summary>
        public static string Content { get; } = RawContent.ReplaceLineEndings(Environment.NewLine);

        private static readonly byte[] StarterFileBytes = CreateStarterFileBytes();

        /// <summary>
        /// Selects the preferred existing snippet file in <paramref name="folder"/>, or publishes
        /// the starter under <paramref name="currentFileName"/> when neither supported name exists.
        /// Engine and Settings callers for the same folder are serialized across processes, and
        /// both names are checked again while that claim is held.
        /// </summary>
        public static FileResult ResolveOrCreateFileInFolder(
            string folder,
            string currentFileName,
            string legacyFileName)
            => ResolveOrCreateFileInFolder(
                folder,
                currentFileName,
                legacyFileName,
                TimeSpan.FromSeconds(FolderClaimTimeoutSeconds),
                beforeStarterPublication: null,
                beforeStarterMove: null);

        internal static FileResult ResolveOrCreateFileInFolder(
            string folder,
            string currentFileName,
            string legacyFileName,
            TimeSpan claimTimeout,
            Action? beforeStarterPublication)
            => ResolveOrCreateFileInFolder(
                folder,
                currentFileName,
                legacyFileName,
                claimTimeout,
                beforeStarterPublication,
                beforeStarterMove: null);

        internal static FileResult ResolveOrCreateFileInFolder(
            string folder,
            string currentFileName,
            string legacyFileName,
            TimeSpan claimTimeout,
            Action? beforeStarterPublication,
            Action? beforeStarterMove)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folder);
            ArgumentException.ThrowIfNullOrWhiteSpace(currentFileName);
            ArgumentException.ThrowIfNullOrWhiteSpace(legacyFileName);
            ArgumentOutOfRangeException.ThrowIfLessThan(claimTimeout, TimeSpan.Zero);

            string fullFolder = Path.GetFullPath(folder);
            Directory.CreateDirectory(fullFolder);

            using IDisposable claim = AcquireFolderClaim(fullFolder, claimTimeout);
            string current = Path.Combine(fullFolder, currentFileName);
            string legacy = Path.Combine(fullFolder, legacyFileName);
            string? existing = FindExisting(current, legacy);
            if (existing is not null)
            {
                return new FileResult(existing, Created: false);
            }

            beforeStarterPublication?.Invoke();

            // Resolution may have happened before this process acquired the folder claim.
            // Check both names again at the publication boundary so a newly arrived legacy
            // database wins instead of being hidden by a starter under the preferred name.
            existing = FindExisting(current, legacy);
            if (existing is not null)
            {
                return new FileResult(existing, Created: false);
            }

            FileResult creation = TryCreateFile(
                current,
                () => !File.Exists(legacy),
                beforeStarterMove,
                beforeStagingCleanup: null,
                beforeStagingCleanupDisposition: null);

            // A sync client or older build may not honor the folder claim. Resolve both names
            // after publication as well. When that race leaves our untouched starter beside a
            // legacy library, the legacy data wins; leaving the starter in place avoids a
            // verify-then-delete race, and normal resolution keeps ignoring that exact starter
            // if this process exits here.
            existing = FindExisting(current, legacy);
            if (existing is not null)
            {
                return new FileResult(
                    existing,
                    creation.Created && string.Equals(existing, current, StringComparison.OrdinalIgnoreCase))
                {
                    StagingCleanup = creation.StagingCleanup,
                };
            }

            throw new IOException($"The Text Expander starter file could not be created in '{fullFolder}'.");
        }

        /// <summary>
        /// Acquires the same cross-process claim used for starter publication. Callers that make
        /// other decisions about the two supported files can hold it through their final
        /// re-resolution, verification, and atomic publication.
        /// </summary>
        public static IDisposable AcquireFolderClaim(string folder)
            => AcquireFolderClaim(folder, TimeSpan.FromSeconds(FolderClaimTimeoutSeconds));

        internal static IDisposable AcquireFolderClaim(string folder, TimeSpan claimTimeout)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folder);
            ArgumentOutOfRangeException.ThrowIfLessThan(claimTimeout, TimeSpan.Zero);

            string fullFolder = Path.GetFullPath(folder);
            var mutex = new Mutex(initiallyOwned: false, FolderClaimName(fullFolder));
            bool ownsClaim;
            try
            {
                try
                {
                    ownsClaim = mutex.WaitOne(claimTimeout);
                }
                catch (AbandonedMutexException)
                {
                    // Re-resolving both names while holding the recovered claim is the required
                    // recovery when the previous process exited mid-operation.
                    ownsClaim = true;
                }

                if (!ownsClaim)
                {
                    throw new IOException($"Timed out waiting to initialize the Text Expander snippet folder '{fullFolder}'.");
                }

                return new FolderClaim(mutex);
            }
            catch
            {
                mutex.Dispose();
                throw;
            }
        }

        internal static string FolderClaimName(string folder)
        {
            string canonical = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)).ToUpperInvariant();
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
            return @"Local\PowerToys.TextExpander.SnippetFolder." + Convert.ToHexString(hash);
        }

        internal static bool IsUntouchedStarterFile(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                return IsUntouchedStarterBytes(bytes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // If the preferred file cannot be proved to be the exact starter, it must retain
                // priority. Treating an unreadable user file as disposable would hide user data.
                return false;
            }
        }

        /// <summary>Returns whether <paramref name="bytes"/> are the exact shared starter file.</summary>
        public static bool IsUntouchedStarterBytes(ReadOnlySpan<byte> bytes)
            => bytes.SequenceEqual(StarterFileBytes);

        private static string? FindExisting(string current, string legacy)
        {
            bool currentExists = File.Exists(current);
            bool legacyExists = File.Exists(legacy);
            if (!currentExists)
            {
                return legacyExists ? legacy : null;
            }

            if (legacyExists && IsUntouchedStarterFile(current))
            {
                return legacy;
            }

            return current;
        }

        /// <summary>
        /// Publishes the starter file only if <paramref name="path"/> is still absent. The complete
        /// payload is written beside the destination and moved into place, so concurrent Settings,
        /// engine, or external creation wins without being overwritten or exposed half-written.
        /// </summary>
        /// <returns><see langword="true"/> when this call published the file.</returns>
        public static bool TryCreateFile(string path)
            => TryCreateFile(
                path,
                canPublish: null,
                beforeMove: null,
                beforeStagingCleanup: null,
                beforeStagingCleanupDisposition: null).Created;

        internal static FileResult TryCreateFile(
            string path,
            Func<bool>? canPublish,
            Action? beforeMove,
            Action<string>? beforeStagingCleanup,
            Action<string>? beforeStagingCleanupDisposition)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            if (File.Exists(path))
            {
                return new FileResult(path, Created: false);
            }

            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            bool created = false;
            HandleBoundFileDeletionResult? stagingCleanup = null;
            try
            {
                File.WriteAllText(temporaryPath, Content, Encoding.UTF8);
                if (canPublish is null || canPublish())
                {
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

            return new FileResult(path, created)
            {
                StagingCleanup = stagingCleanup,
            };
        }

        private static byte[] CreateStarterFileBytes()
        {
            byte[] preamble = Encoding.UTF8.GetPreamble();
            byte[] content = Encoding.UTF8.GetBytes(Content);
            byte[] result = new byte[preamble.Length + content.Length];
            Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
            Buffer.BlockCopy(content, 0, result, preamble.Length, content.Length);
            return result;
        }

        /// <summary>The snippet file selected or published for a folder.</summary>
        public readonly record struct FileResult(string FilePath, bool Created)
        {
            public HandleBoundFileDeletionResult? StagingCleanup { get; init; }

            public bool StagingCleanupSucceeded => !StagingCleanup.HasValue || StagingCleanup.Value.Succeeded;

            public string? IncompleteStagingPath
                => StagingCleanup.HasValue && !StagingCleanup.Value.Succeeded
                    ? StagingCleanup.Value.Path
                    : null;
        }

        private sealed class FolderClaim : IDisposable
        {
            private Mutex? _mutex;

            public FolderClaim(Mutex mutex)
            {
                _mutex = mutex;
            }

            public void Dispose()
            {
                Mutex? mutex = Interlocked.Exchange(ref _mutex, null);
                if (mutex is null)
                {
                    return;
                }

                mutex.ReleaseMutex();
                mutex.Dispose();
            }
        }
    }
}
