// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.TextExpander.Storage;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    public sealed class TextExpanderStorageEnvironment : ITextExpanderStorageEnvironment
    {
        private readonly TimeSpan _pollInterval;

        public TextExpanderStorageEnvironment()
            : this(TimeSpan.FromSeconds(2))
        {
        }

        internal TextExpanderStorageEnvironment(TimeSpan pollInterval)
        {
            _pollInterval = pollInterval;
        }

        public string DefaultSnippetsFolder => Path.Combine(
            Helper.LocalApplicationDataFolder(),
            "Microsoft",
            "PowerToys",
            TextExpanderSettings.ModuleName);

        public string ExpandEnvironmentVariables(string value) => Environment.ExpandEnvironmentVariables(value);

        public bool FileExists(string path) => File.Exists(path);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public void WriteNewFile(string path, byte[] bytes)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }

        public void MoveFile(string source, string destination) => File.Move(source, destination, overwrite: false);

        public void ReplaceFile(string source, string destination, string backup)
            => File.Replace(source, destination, backup, ignoreMetadataErrors: true);

        public HandleBoundFileDeletionResult DeleteOwnedFile(string path, byte[] expectedBytes)
            => HandleBoundFileDeletion.TryDelete(path, expectedBytes);

        public IDisposable WatchForFiles(string folder, IReadOnlyList<string> fileNames, Action changed)
            => new FileArrivalWatcher(folder, fileNames, changed, _pollInterval);

        /// <summary>
        /// Calls back when a watched file changes. A FileSystemWatcher misses files in a folder that didn't exist
        /// yet, on shares that don't report changes, and after it overflows or fails, so a timer also compares
        /// each file's size and write time. It does that off the UI thread and calls back only when they differ,
        /// because the callback reads the files on the UI thread.
        /// </summary>
        private sealed class FileArrivalWatcher : IDisposable
        {
            private readonly FileInfo[] _files;
            private readonly Action _changed;
            private readonly Timer _timer;
            private FileStamp[] _observed;
            private int _polling;
            private FileSystemWatcher _watcher;

            public FileArrivalWatcher(string folder, IReadOnlyList<string> fileNames, Action changed, TimeSpan pollInterval)
            {
                _files = fileNames.Select(name => new FileInfo(Path.Combine(folder, name))).ToArray();
                _changed = changed;
                _observed = Observe();
                _timer = new Timer(_ => Poll(), null, pollInterval, pollInterval);

                try
                {
                    if (Directory.Exists(folder))
                    {
                        var watchedNames = new HashSet<string>(fileNames, StringComparer.OrdinalIgnoreCase);
                        _watcher = new FileSystemWatcher(folder)
                        {
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                            Filter = "*",
                        };

                        void OnFileEvent(object sender, FileSystemEventArgs e)
                        {
                            if (watchedNames.Contains(e.Name))
                            {
                                changed();
                            }
                        }

                        void OnRenamed(object sender, RenamedEventArgs e)
                        {
                            if (watchedNames.Contains(e.Name) || watchedNames.Contains(e.OldName))
                            {
                                changed();
                            }
                        }

                        _watcher.Created += OnFileEvent;
                        _watcher.Deleted += OnFileEvent;
                        _watcher.Changed += OnFileEvent;
                        _watcher.Renamed += OnRenamed;
                        _watcher.Error += (_, _) => changed();
                        _watcher.EnableRaisingEvents = true;
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException
                                              or PlatformNotSupportedException or System.ComponentModel.Win32Exception
                                              or System.Security.SecurityException)
                {
                    _watcher?.Dispose();
                    _watcher = null;
                }
            }

            public void Dispose()
            {
                _watcher?.Dispose();
                _timer.Dispose();
            }

            private void Poll()
            {
                // A tick that comes while a slow share is still answering the last one is skipped.
                if (Interlocked.Exchange(ref _polling, 1) != 0)
                {
                    return;
                }

                try
                {
                    FileStamp[] observed = Observe();
                    if (!observed.AsSpan().SequenceEqual(_observed))
                    {
                        _observed = observed;
                        _changed();
                    }
                }
                finally
                {
                    Volatile.Write(ref _polling, 0);
                }
            }

            private FileStamp[] Observe()
            {
                var observed = new FileStamp[_files.Length];
                for (int i = 0; i < _files.Length; i++)
                {
                    FileInfo file = _files[i];
                    file.Refresh();
                    observed[i] = file.Exists ? new FileStamp(true, file.Length, file.LastWriteTimeUtc) : default;
                }

                return observed;
            }

            private readonly record struct FileStamp(bool Exists, long Length, DateTime LastWriteTimeUtc);
        }
    }
}
