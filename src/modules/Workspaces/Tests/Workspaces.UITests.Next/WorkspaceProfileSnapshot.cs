// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Microsoft.Workspaces.UITests
{
    internal sealed class WorkspaceProfileSnapshot : IDisposable
    {
        private const string StageMarker = "PowerToys.Workspaces.UITests.Next.v2";
        private const string ZipMarker = "PowerToys.Workspaces.UITests.Next.v1";
        private const string MissingSuffix = ".absent";
        private const string RequestPrefix = "temp-workspaces.json.";
        private static readonly string[] FileNames = ["workspaces.json", "editor-window.json", "settings.json", "temp-workspaces.json"];
        private readonly string workspaceDirectory;
        private readonly string recoveryDirectory;
        private readonly string capturing;
        private readonly string original;
        private readonly FileStream lease;
        private bool isolated;
        private bool disposed;

        internal WorkspaceProfileSnapshot(string workspaceDirectory)
        {
            this.workspaceDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceDirectory));
            recoveryDirectory = this.workspaceDirectory + ".UITestRecovery";
            Directory.CreateDirectory(recoveryDirectory);
            RequireRegularDirectory(recoveryDirectory);
            capturing = Path.Combine(recoveryDirectory, "capturing");
            original = Path.Combine(recoveryDirectory, "original");
            lease = new FileStream(Path.Combine(recoveryDirectory, "profile.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        // The class lease precedes Runner startup. The product lock only covers the handoff of the files.
        internal void Isolate()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (isolated)
            {
                return;
            }

            Directory.CreateDirectory(workspaceDirectory);
            using var transaction = AcquireStoreLock();
            var legacyZip = Path.Combine(recoveryDirectory, "original.zip");
            bool legacy = RequireRegularFileOrMissing(legacyZip);
            bool partial = Directory.Exists(capturing);
            bool committed = Directory.Exists(original);
            if ((partial && committed) || (legacy && (partial || committed)))
            {
                throw new InvalidDataException($"Conflicting Workspaces UI test backups in '{recoveryDirectory}'.");
            }

            if (partial)
            {
                RestoreStage(capturing, partialCapture: true, interrupted: true);
            }
            else if (committed)
            {
                RestoreStage(original, partialCapture: false, interrupted: true);
            }
            else if (legacy)
            {
                RestoreLegacyZip(legacyZip);
            }

            // Enumerate before moving anything; mutations during a directory enumeration can skip requests.
            var requests = new DirectoryInfo(workspaceDirectory).GetFiles(RequestPrefix + "{*}")
                .Where(file => IsRequestName(file.Name) && !file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                .Select(file => file.Name).ToArray();
            Directory.CreateDirectory(capturing);
            try
            {
                File.Open(Path.Combine(capturing, StageMarker), FileMode.CreateNew).Dispose();

                foreach (var name in FileNames.Concat(requests))
                {
                    var path = Path.Combine(workspaceDirectory, name);
                    if (RequireRegularFileOrMissing(path))
                    {
                        Rename(path, Path.Combine(capturing, name));
                    }
                    else if (IsFixedName(name))
                    {
                        File.Open(Path.Combine(capturing, name + MissingSuffix), FileMode.CreateNew).Dispose();
                    }
                }

                Directory.Move(capturing, original);
            }
            catch
            {
                // A failed move leaves its source untouched. Only the files already in capturing need undoing.
                RestoreStage(capturing, partialCapture: true, interrupted: true);
                throw;
            }

            isolated = true;
        }

        // A writer that failed to exit could overwrite restored user data. The next host performs recovery.
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
                    Directory.CreateDirectory(workspaceDirectory);
                    using var transaction = AcquireStoreLock();
                    RestoreStage(original, partialCapture: false, interrupted: false);
                }
            }
            finally
            {
                disposed = true;
                lease.Dispose();
            }
        }

        private FileStream AcquireStoreLock()
        {
            // Match the native and managed Workspaces writers' access, sharing and two-second retry budget.
            var path = Path.Combine(workspaceDirectory, "workspaces.json.lock");
            var timer = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException error) when ((error.HResult & 0xFFFF) == 32 && timer.ElapsedMilliseconds < 2000)
                {
                    Thread.Sleep(20);
                }
            }
        }

        private static bool IsFixedName(string name) => FileNames.Contains(name, StringComparer.Ordinal);

        private static bool IsRequestName(string name) =>
            name.Length == RequestPrefix.Length + 38 &&
            name.StartsWith(RequestPrefix, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParseExact(name.AsSpan(RequestPrefix.Length), "B", out _);

        // A null source represents an originally absent fixed file. Validate the whole journal before using it.
        private static Dictionary<string, string?> ReadStage(string stage)
        {
            RequireRegularDirectory(stage);
            var pending = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            bool marked = false;
            foreach (var entry in new DirectoryInfo(stage).GetFileSystemInfos())
            {
                if (entry is FileInfo file && !file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    if (file.Name == StageMarker && file.Length == 0)
                    {
                        marked = true;
                        continue;
                    }

                    if ((IsFixedName(file.Name) || IsRequestName(file.Name)) && pending.TryAdd(file.Name, file.FullName))
                    {
                        continue;
                    }

                    if (file.Name.EndsWith(MissingSuffix, StringComparison.Ordinal) && file.Length == 0)
                    {
                        var name = file.Name[..^MissingSuffix.Length];
                        if (IsFixedName(name) && pending.TryAdd(name, null))
                        {
                            continue;
                        }
                    }
                }

                throw new InvalidDataException($"Unrecognized or duplicate Workspaces backup entry '{entry.FullName}'.");
            }

            // An empty directory can be left just before creating or just after retiring the marker.
            if (!marked && pending.Count != 0)
            {
                throw new InvalidDataException($"Unrecognized Workspaces UI test backup at '{stage}'.");
            }

            return pending;
        }

        private void RestoreStage(string stage, bool partialCapture, bool interrupted)
        {
            var pending = ReadStage(stage);
            var interruptedDirectory = NewInterruptedDirectoryName();
            foreach (var (name, source) in pending)
            {
                var path = Path.Combine(workspaceDirectory, name);
                if (source is not null || !partialCapture)
                {
                    // A request with the original ID may have appeared since recovery began.
                    DisplaceCurrent(path, interrupted || IsRequestName(name) ? interruptedDirectory : null);
                }

                if (source is not null)
                {
                    // This rename is also the durable completion record for a later retry.
                    Rename(source, path);
                }
                else
                {
                    File.Delete(Path.Combine(stage, name + MissingSuffix));
                }
            }

            File.Delete(Path.Combine(stage, StageMarker));
            Directory.Delete(stage);
        }

        private string NewInterruptedDirectoryName() =>
            Path.Combine(recoveryDirectory, $"interrupted-{DateTime.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}");

        private static void DisplaceCurrent(string path, string? interruptedDirectory)
        {
            if (RequireRegularFileOrMissing(path))
            {
                if (interruptedDirectory is null)
                {
                    File.Delete(path);
                }
                else
                {
                    Directory.CreateDirectory(interruptedDirectory);
                    RequireRegularDirectory(interruptedDirectory);
                    Rename(path, Path.Combine(interruptedDirectory, Path.GetFileName(path)));
                }
            }
        }

        private void RestoreLegacyZip(string path)
        {
            using (var archive = ZipFile.OpenRead(path))
            {
                var entries = archive.Entries.ToDictionary(entry => entry.FullName, StringComparer.OrdinalIgnoreCase);
                if (!entries.Remove(ZipMarker, out var marker) || marker.FullName != ZipMarker || marker.Length != 0 ||
                    entries.Keys.Any(name => !IsFixedName(name) && !IsRequestName(name)))
                {
                    throw new InvalidDataException($"Unrecognized Workspaces UI test backup at '{path}'.");
                }

                var requestTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in entries.Values)
                {
                    // Fail on unreadable compressed data before displacing any live files.
                    using var source = entry.Open();
                    source.CopyTo(Stream.Null);
                    if (IsRequestName(entry.FullName))
                    {
                        if (!DateTime.TryParseExact(entry.Comment, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var modified) || modified.Kind != DateTimeKind.Utc)
                        {
                            throw new InvalidDataException($"Missing UTC write time for '{entry.FullName}' in '{path}'.");
                        }

                        requestTimes.Add(entry.FullName, modified);
                    }
                }

                var interruptedDirectory = NewInterruptedDirectoryName();
                foreach (var name in FileNames.Concat(requestTimes.Keys))
                {
                    var destination = Path.Combine(workspaceDirectory, name);
                    if (!entries.TryGetValue(name, out var entry))
                    {
                        DisplaceCurrent(destination, interruptedDirectory);
                        continue;
                    }

                    DateTime? requestWriteTime = requestTimes.TryGetValue(name, out var time) ? time : null;
                    if (MatchesLegacyEntry(destination, entry, requestWriteTime))
                    {
                        // The v1 host may have stopped after creating the ZIP but before removing this original.
                        continue;
                    }

                    var temporary = Path.Combine(recoveryDirectory, Guid.NewGuid().ToString("N") + ".tmp");
                    var output = File.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    try
                    {
                        using (output)
                        using (var source = entry.Open())
                        {
                            source.CopyTo(output);
                            output.Flush(flushToDisk: true);
                        }

                        File.SetLastWriteTimeUtc(temporary, requestWriteTime ?? entry.LastWriteTime.UtcDateTime);
                        DisplaceCurrent(destination, interruptedDirectory);
                        Rename(temporary, destination);
                    }
                    finally
                    {
                        File.Delete(temporary);
                    }
                }
            }

            File.Delete(path);
        }

        private static bool MatchesLegacyEntry(string path, ZipArchiveEntry entry, DateTime? requestWriteTime)
        {
            if (!RequireRegularFileOrMissing(path))
            {
                return false;
            }

            using var live = File.OpenRead(path);
            if (live.Length != entry.Length || (requestWriteTime is DateTime time && File.GetLastWriteTimeUtc(live.SafeFileHandle) != time))
            {
                return false;
            }

            using var archived = entry.Open();
            Span<byte> actual = stackalloc byte[4096];
            Span<byte> expected = stackalloc byte[4096];
            int count;
            while ((count = live.Read(actual)) > 0)
            {
                archived.ReadExactly(expected[..count]);
                if (!actual[..count].SequenceEqual(expected[..count]))
                {
                    return false;
                }
            }

            return archived.ReadByte() == -1;
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

        private static void RequireRegularDirectory(string path)
        {
            if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != FileAttributes.Directory)
            {
                throw new IOException($"Expected a regular Workspaces recovery directory at '{path}'.");
            }
        }

        private static void Rename(string source, string destination)
        {
            // File.Move permits copying across volumes. A rename must preserve the original file and its NTFS metadata.
            if (!MoveFileEx(source, destination, 0))
            {
                throw new IOException($"Could not move '{source}' to '{destination}'.", new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }

        [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MoveFileEx(string existingFileName, string newFileName, uint flags);
    }
}
