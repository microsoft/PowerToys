// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Compression;

namespace Microsoft.Workspaces.UITests
{
    internal sealed class WorkspaceProfileSnapshot : IDisposable
    {
        private const string FormatMarker = "PowerToys.Workspaces.UITests.Next.v1";
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
                Capture(Path.Combine(recoveryDirectory, $"interrupted-{DateTime.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}.zip"));
                Restore();
            }

            Capture(original);
            try
            {
                foreach (var name in FileNames)
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
                    Restore();
                }
            }
            finally
            {
                disposed = true;
                lease.Dispose();
            }
        }

        private void Capture(string destination)
        {
            WriteAtomically(destination, stream =>
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
                archive.CreateEntry(FormatMarker);
                foreach (var name in FileNames)
                {
                    var path = Path.Combine(workspaceDirectory, name);
                    if (RequireRegularFileOrMissing(path))
                    {
                        archive.CreateEntryFromFile(path, name);
                    }
                }
            });
        }

        private void Restore()
        {
            Directory.CreateDirectory(workspaceDirectory);
            using (var archive = ZipFile.OpenRead(original))
            {
                var entries = archive.Entries.ToDictionary(entry => entry.FullName, StringComparer.Ordinal);
                if (!entries.Remove(FormatMarker, out var marker) || marker.Length != 0 ||
                    entries.Keys.Except(FileNames, StringComparer.Ordinal).Any())
                {
                    throw new InvalidDataException($"Unrecognized Workspaces UI test backup at '{original}'.");
                }

                foreach (var name in FileNames)
                {
                    var path = Path.Combine(workspaceDirectory, name);
                    RequireRegularFileOrMissing(path);
                    if (entries.TryGetValue(name, out var entry))
                    {
                        WriteAtomically(path, destination =>
                        {
                            using var source = entry.Open();
                            source.CopyTo(destination);
                        }, overwrite: true);
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
