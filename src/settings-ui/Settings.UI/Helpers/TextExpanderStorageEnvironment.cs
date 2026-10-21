// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public sealed class TextExpanderStorageEnvironment : ITextExpanderStorageEnvironment
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

        public string DefaultSnippetsFolder => Path.Combine(
            Helper.LocalApplicationDataFolder(),
            "Microsoft",
            "PowerToys",
            TextExpanderSettings.ModuleName);

        public string ExpandEnvironmentVariables(string value) => Environment.ExpandEnvironmentVariables(value);

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

        public IDisposable WatchForFiles(string folder, IReadOnlyList<string> fileNames, Action changed)
            => new FileArrivalWatcher(folder, fileNames, changed);

        private sealed class FileArrivalWatcher : IDisposable
        {
            private readonly Timer _timer;
            private FileSystemWatcher _watcher;

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
