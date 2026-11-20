// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.IO.Compression;

namespace Microsoft.Workspaces.UITests
{
    internal sealed class WorkspaceProfileSnapshot : IDisposable
    {
        private const string FormatMarker = "PowerToys.Workspaces.UITests.Next.v1";
        private const string RequestPrefix = "temp-workspaces.json.";
        private static readonly string[] FileNames = ["workspaces.json", "editor-window.json", "settings.json", "temp-workspaces.json"];
        private readonly string workspaceDirectory;
        private readonly string recoveryDirectory;
        private readonly string original;
        private readonly FileStream lease;
        private bool isolated;
        private bool disposed;

        internal WorkspaceProfileSnapshot(string workspaceDirectory)
        {
            this.workspaceDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceDirectory));
            recoveryDirectory = this.workspaceDirectory + ".UITestRecovery";
            Directory.CreateDirectory(recoveryDirectory);
            original = Path.Combine(recoveryDirectory, "original.zip");
            lease = new FileStream(Path.Combine(recoveryDirectory, "profile.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        // Acquire the lease before either testhost can touch Runner. Isolate only after the previous
        // writers have stopped. The same original is retained across all cases in this class.
        internal void Isolate()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (isolated)
            {
                return;
            }

            Directory.CreateDirectory(workspaceDirectory);
            if (File.Exists(original))
            {
                // A terminated host left its original on disk. Keep any later edits separately,
                // including changes the user made before starting this new run.
                CaptureInterruptedProfile();
                Restore();
            }

            var names = Capture(original);
            try
            {
                foreach (var name in names)
                {
                    var path = Path.Combine(workspaceDirectory, name);
                    RequireRegularFileOrMissing(path);
                    File.Delete(path);
                }
            }
            catch
            {
                Restore();
                throw;
            }

            isolated = true;
        }

        // A writer that failed to exit could overwrite restored user data. Leave it on disk
        // until the next run has successfully stopped the module processes.
        internal void LeaveForRecovery() => isolated = false;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            try
            {
                if (isolated)
                {
                    Restore(preserveReplacedRequests: true);
                }
            }
            finally
            {
                disposed = true;
                lease.Dispose();
            }
        }

        private void CaptureInterruptedProfile() => Capture(Path.Combine(recoveryDirectory, $"interrupted-{DateTime.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}.zip"));

        private List<string> Capture(string destination)
        {
            var directory = new DirectoryInfo(workspaceDirectory);
            var requests = directory.EnumerateFiles(RequestPrefix + "{*}", SearchOption.TopDirectoryOnly)
                .Where(file => IsRequestName(file.Name) && !file.Attributes.HasFlag(FileAttributes.ReparsePoint));
            List<string> captured = [];
            WriteAtomically(destination, stream =>
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
                archive.CreateEntry(FormatMarker);
                foreach (var name in FileNames.Concat(requests.Select(file => file.Name)))
                {
                    var path = Path.Combine(workspaceDirectory, name);
                    if (RequireRegularFileOrMissing(path))
                    {
                        var entry = archive.CreateEntryFromFile(path, name);
                        if (IsRequestName(name))
                        {
                            // ZIP timestamps have two-second precision. Editor expires requests by their UTC write time.
                            entry.Comment = File.GetLastWriteTimeUtc(path).ToString("O", CultureInfo.InvariantCulture);
                        }

                        captured.Add(name);
                    }
                }
            });
            return captured;
        }

        private static bool IsRequestName(string name) =>
            name.Length == RequestPrefix.Length + 38 &&
            name.StartsWith(RequestPrefix, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParseExact(name.AsSpan(RequestPrefix.Length), "B", out _);

        private void Restore(bool preserveReplacedRequests = false)
        {
            Directory.CreateDirectory(workspaceDirectory);
            using (var archive = ZipFile.OpenRead(original))
            {
                var entries = archive.Entries.ToDictionary(entry => entry.FullName, StringComparer.Ordinal);
                if (!entries.Remove(FormatMarker, out var marker) || marker.Length != 0 ||
                    entries.Keys.Except(FileNames, StringComparer.Ordinal).Any(name => !IsRequestName(name)))
                {
                    throw new InvalidDataException($"Unrecognized Workspaces UI test backup at '{original}'.");
                }

                var requests = entries.Keys.Where(IsRequestName).ToArray();
                if (preserveReplacedRequests && requests.Any(name => RequireRegularFileOrMissing(Path.Combine(workspaceDirectory, name))))
                {
                    // Someone recreated a saved request ID during the run. Keep both versions.
                    CaptureInterruptedProfile();
                }

                // Leave any newly created request IDs in place.
                foreach (var name in FileNames.Concat(requests))
                {
                    var path = Path.Combine(workspaceDirectory, name);
                    RequireRegularFileOrMissing(path);
                    if (entries.TryGetValue(name, out var entry))
                    {
                        bool isRequest = IsRequestName(name);
                        DateTime modified = default;
                        if (isRequest &&
                            (!DateTime.TryParseExact(entry.Comment, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out modified) || modified.Kind != DateTimeKind.Utc))
                        {
                            throw new InvalidDataException($"Missing UTC write time for '{name}' in '{original}'.");
                        }

                        using var source = entry.Open();
                        WriteAtomically(path, source.CopyTo, overwrite: true);
                        if (isRequest)
                        {
                            File.SetLastWriteTimeUtc(path, modified);
                        }
                    }
                    else
                    {
                        File.Delete(path);
                    }
                }
            }

            // Keep the entire backup if even one restore failed.
            File.Delete(original);
        }

        private static bool RequireRegularFileOrMissing(string path)
        {
            try
            {
                if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                {
                    throw new IOException($"Expected a regular Workspaces data file at '{path}'.");
                }

                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        private static void WriteAtomically(string path, Action<Stream> write, bool overwrite = false)
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            try
            {
                using (stream)
                {
                    write(stream);
                    stream.Flush(flushToDisk: true);
                }

                File.Move(temporary, path, overwrite);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }
}
