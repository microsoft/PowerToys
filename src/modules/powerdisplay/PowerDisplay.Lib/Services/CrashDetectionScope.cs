// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using ManagedCommon;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// IDisposable scope that writes <c>discovery.lock</c> on Begin() and deletes it on Dispose().
    /// Wrap DDC/CI capability fetch (Phase 2 of monitor discovery) in a using-block to mark
    /// "we are inside the dangerous code path." If the process is killed externally (BSOD,
    /// TerminateProcess, FailFast) Dispose() never runs and the lock survives — at next
    /// PowerDisplay.exe startup CrashRecovery detects the orphan and disables the module.
    ///
    /// On normal completion or .NET exception, Dispose() removes the lock.
    /// </summary>
    public sealed partial class CrashDetectionScope : IDisposable
    {
        private readonly string _lockPath;
        private int _disposedFlag;

        /// <summary>
        /// Begin a new scope. Writes the lock atomically via "write temp + rename":
        /// the lock file at <paramref name="lockPath"/> either exists fully written or
        /// does not exist — a partial/empty file is impossible. Uses WriteThrough +
        /// Flush(true) for L3 durability so that an immediate BSOD preserves the lock.
        /// Throws on any IO failure; the caller should not enter Phase 2 if Begin() throws.
        /// </summary>
        /// <param name="lockPath">Override the default lock path. Defaults to
        /// <see cref="PathConstants.DiscoveryLockPath"/>. Test code passes a temp path.</param>
        public static CrashDetectionScope Begin(string? lockPath = null)
        {
            var path = lockPath ?? PathConstants.DiscoveryLockPath;

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var payload = JsonSerializer.SerializeToUtf8Bytes(
                new LockPayload(
                    Version: 1,
                    Pid: Environment.ProcessId,
                    StartedAt: DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                LockPayloadJsonContext.Default.LockPayload);

            // Atomic publish: write to .tmp, fsync, then rename to the real lock path.
            // Any failure during write only leaves a .tmp (which Phase 0 ignores), so a
            // partial write can never cause a false-positive quarantine on next start.
            // File.Move is atomic on the same volume and fails if the destination already
            // exists — preserving the original FileMode.CreateNew defense against duplicate
            // scopes or a missed cleanup from a previous run.
            var tempPath = path + ".tmp";
            try
            {
                // FileMode.Create overwrites any leftover .tmp from a previous failed attempt.
                using (var fs = new FileStream(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    options: FileOptions.WriteThrough))
                {
                    fs.Write(payload);
                    fs.Flush(flushToDisk: true);
                }

                File.Move(tempPath, path);
            }
            catch
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Best-effort cleanup; leftover .tmp is harmless (Phase 0 only looks at path).
                }

                throw;
            }

            Logger.LogInfo($"CrashDetectionScope: lock written at {path}");
            return new CrashDetectionScope(path);
        }

        private CrashDetectionScope(string lockPath)
        {
            _lockPath = lockPath;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposedFlag, 1) != 0)
            {
                return;
            }

            try
            {
                File.Delete(_lockPath);
                Logger.LogInfo($"CrashDetectionScope: lock deleted at {_lockPath}");
            }
            catch (Exception ex)
            {
                // Worst case: false-positive quarantine on next start, recoverable via Ignore.
                Logger.LogWarning($"CrashDetectionScope: failed to delete lock at {_lockPath}: {ex.Message}");
            }
        }

        // System.Text.Json record for the lock payload.
        internal sealed record LockPayload(int Version, int Pid, string StartedAt);

        [JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
        [JsonSerializable(typeof(LockPayload))]
        internal sealed partial class LockPayloadJsonContext : JsonSerializerContext
        {
        }
    }
}
