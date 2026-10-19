// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Security;
using System.Threading;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>The real file system and environment, for the Text Expander OneDrive setup.</summary>
    public sealed class TextExpanderSyncEnvironment : ITextExpanderSyncEnvironment
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

        // Hidden and system entries too: the search decides what to skip.
        private static readonly EnumerationOptions ListOptions = new() { IgnoreInaccessible = true, AttributesToSkip = 0 };

        public string MachineName => Environment.MachineName;

        public string DefaultSnippetsFolder => Path.Combine(
            Helper.LocalApplicationDataFolder(),
            "Microsoft",
            "PowerToys",
            TextExpanderSettings.ModuleName);

        public string GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

        public string ExpandEnvironmentVariables(string value) => Environment.ExpandEnvironmentVariables(value);

        public bool DirectoryExists(string path) => Directory.Exists(path);

        public bool FileExists(string path) => File.Exists(path);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

        public void WriteNewFile(string path, byte[] bytes)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }

        public void MoveFile(string source, string destination) => File.Move(source, destination, overwrite: false);

        public void ReplaceFile(string source, string destination, string backup)
            => File.Replace(source, destination, backup, ignoreMetadataErrors: true);

        public void DeleteFile(string path) => File.Delete(path);

        public DateTime GetLastWriteTime(string path) => File.GetLastWriteTime(path);

        public FileAttributes GetAttributes(string path) => File.GetAttributes(path);

        public void SetAttributes(string path, FileAttributes attributes) => File.SetAttributes(path, attributes);

        public string FindOneDriveExecutable()
        {
            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "OneDrive", "OneDrive.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft OneDrive", "OneDrive.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft OneDrive", "OneDrive.exe"),
            };

            foreach (string candidate in candidates)
            {
                if (Path.IsPathFullyQualified(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        public void StartProcess(string path)
        {
            using Process process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = false });
        }

        public IDisposable WatchForFiles(string folder, IReadOnlyList<string> fileNames, Action changed)
            => new FileArrivalWatcher(folder, fileNames, changed);

        public IReadOnlyList<TextExpanderFolderEntry> ListFolder(string folder, IReadOnlyList<string> fileNames) => List(folder, fileNames);

        /// <summary>The listing <see cref="ListFolder"/> returns, for any folder.</summary>
        internal static IReadOnlyList<TextExpanderFolderEntry> List(string folder, IReadOnlyList<string> fileNames)
        {
            var entries = new List<TextExpanderFolderEntry>();
            try
            {
                var listing = new FileSystemEnumerable<TextExpanderFolderEntry>(
                    folder,
                    (ref FileSystemEntry entry) => new TextExpanderFolderEntry(
                        entry.FileName.ToString(),
                        entry.IsDirectory,
                        entry.Attributes,
                        entry.IsDirectory && (entry.Attributes & FileAttributes.ReparsePoint) != 0 && IsLink(entry.ToFullPath()),
                        entry.LastWriteTimeUtc.LocalDateTime),
                    ListOptions)
                {
                    ShouldIncludePredicate = (ref FileSystemEntry entry) => entry.IsDirectory || IsOneOf(entry.FileName, fileNames),
                };
                entries.AddRange(listing);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or SecurityException)
            {
                // A folder that can't be listed has nothing to offer.
            }

            return entries;
        }

        private static bool IsOneOf(ReadOnlySpan<char> name, IReadOnlyList<string> names)
        {
            foreach (string candidate in names)
            {
                if (name.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets a value indicating whether a folder is a symbolic link or junction. Every OneDrive
        /// folder is a reparse point, a cloud placeholder, so only a link target tells them apart.
        /// A folder that can't be checked counts as a link, so a search stays out of it.
        /// </summary>
        private static bool IsLink(string path)
        {
            try
            {
                return new DirectoryInfo(path).LinkTarget != null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or SecurityException)
            {
                return true;
            }
        }

        /// <summary>
        /// A FileSystemWatcher reacts quickly once the folder exists. Polling covers a folder that
        /// OneDrive has not created yet and any event the watcher misses.
        /// </summary>
        private sealed class FileArrivalWatcher : IDisposable
        {
            private readonly Timer _timer;
            private readonly FileSystemWatcher _watcher;

            public FileArrivalWatcher(string folder, IReadOnlyList<string> fileNames, Action changed)
            {
                _timer = new Timer(_ => changed(), null, PollInterval, PollInterval);

                try
                {
                    if (Directory.Exists(folder))
                    {
                        _watcher = new FileSystemWatcher(folder)
                        {
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                        };
                        foreach (string fileName in fileNames)
                        {
                            _watcher.Filters.Add(fileName);
                        }

                        _watcher.Created += (_, _) => changed();
                        _watcher.Renamed += (_, _) => changed();
                        _watcher.Changed += (_, _) => changed();
                        _watcher.EnableRaisingEvents = true;
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or PlatformNotSupportedException)
                {
                    // Polling still notices the file.
                    _watcher?.Dispose();
                    _watcher = null;
                }
            }

            public void Dispose()
            {
                _watcher?.Dispose();
                _timer.Dispose();
            }
        }
    }
}
