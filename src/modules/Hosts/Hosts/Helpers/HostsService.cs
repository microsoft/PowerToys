// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hosts.Models;

namespace Hosts.Helpers
{
    public class HostsService : IDisposable
    {
        private static SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);
        private readonly IFileSystem _fileSystem;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly List<string> _invalidLines;
        private bool _backupDone;
        private bool _disposed;

        public static string HostsFilePath { get; } = @"C:\Windows\System32\drivers\etc\hosts";

        public static string BackupSuffix { get; } = $"_PowerToysBackup_";

        public event EventHandler FileChanged;

        public HostsService(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
            _invalidLines = new List<string>();

            _fileSystemWatcher = _fileSystem.FileSystemWatcher.CreateNew();
            _fileSystemWatcher.Path = _fileSystem.Path.GetDirectoryName(HostsFilePath);
            _fileSystemWatcher.Filter = _fileSystem.Path.GetFileName(HostsFilePath);
            _fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite;
            _fileSystemWatcher.Changed += FileSystemWatcher_Changed;
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            _fileSystemWatcher.EnableRaisingEvents = false;
            FileChanged?.Invoke(this, EventArgs.Empty);
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        public bool Exists()
        {
            return _fileSystem.File.Exists(HostsFilePath);
        }

        public async Task<List<Entry>> ReadAsync()
        {
            _invalidLines.Clear();
            var result = new List<Entry>();

            if (!Exists())
            {
                return result;
            }

            var lines = await _fileSystem.File.ReadAllLinesAsync(HostsFilePath);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var entry = new Entry(line);

                if (entry.Valid)
                {
                    result.Add(entry);
                }
                else
                {
                    _invalidLines.Add(line);
                }
            }

            return result;
        }

        public async Task<bool> WriteAsync(IEnumerable<Entry> entries)
        {
            var lines = new List<string>();

            if (entries.Any())
            {
                var addressPadding = entries.Where(e => !string.IsNullOrWhiteSpace(e.Address)).Max(e => e.Address.Length) + 1;
                var hostsPadding = entries.Where(e => !string.IsNullOrWhiteSpace(e.Hosts)).Max(e => e.Hosts.Length) + 1;
                var anyDisabled = entries.Any(e => !e.Active);

                foreach (var line in _invalidLines)
                {
                    lines.Add(line);
                }

                foreach (var e in entries)
                {
                    var lineBuilder = new StringBuilder();

                    if (!e.Valid)
                    {
                        lineBuilder.Append(e.GetLine());
                    }
                    else
                    {
                        if (!e.Active)
                        {
                            lineBuilder.Append("# ");
                        }
                        else if (anyDisabled)
                        {
                            lineBuilder.Append("  ");
                        }

                        lineBuilder.Append(e.Address.PadRight(addressPadding));
                        lineBuilder.Append(string.Join(' ', e.Hosts).PadRight(hostsPadding));

                        if (e.Comment != string.Empty)
                        {
                            lineBuilder.Append("# ");
                            lineBuilder.Append(e.Comment);
                        }

                        lines.Add(lineBuilder.ToString().TrimEnd());
                    }
                }
            }

            try
            {
                await _asyncLock.WaitAsync();
                _fileSystemWatcher.EnableRaisingEvents = false;

                if (!_backupDone)
                {
                    _fileSystem.File.Copy(HostsFilePath, HostsFilePath + BackupSuffix + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture));
                    _backupDone = true;
                }

                await _fileSystem.File.WriteAllLinesAsync(HostsFilePath, lines);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _fileSystemWatcher.EnableRaisingEvents = true;
                _asyncLock.Release();
            }

            return true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _asyncLock.Dispose();
                    _disposed = true;
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
