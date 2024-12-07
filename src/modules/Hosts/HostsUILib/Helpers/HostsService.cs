// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using HostsUILib.Exceptions;
using HostsUILib.Models;
using HostsUILib.Settings;
using Microsoft.Win32;

namespace HostsUILib.Helpers
{
    public class HostsService : IHostsService, IDisposable
    {
        private const string _backupSuffix = $"_PowerToysBackup_";
        private const int _defaultBufferSize = 4096; // From System.IO.File source code

        private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);
        private readonly IFileSystem _fileSystem;
        private readonly IUserSettings _userSettings;
        private readonly IElevationHelper _elevationHelper;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly string _hostsFilePath;
        private bool _backupDone;
        private bool _disposed;

        public string HostsFilePath => _hostsFilePath;

        public event EventHandler FileChanged;

        public Encoding Encoding => _userSettings.Encoding == HostsEncoding.Utf8 ? new UTF8Encoding(false) : new UTF8Encoding(true);

        public HostsService(
            IFileSystem fileSystem,
            IUserSettings userSettings,
            IElevationHelper elevationHelper)
        {
            _fileSystem = fileSystem;
            _userSettings = userSettings;
            _elevationHelper = elevationHelper;

            _hostsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\drivers\etc\hosts");

            _fileSystemWatcher = _fileSystem.FileSystemWatcher.New();
            _fileSystemWatcher.Path = _fileSystem.Path.GetDirectoryName(HostsFilePath);
            _fileSystemWatcher.Filter = _fileSystem.Path.GetFileName(HostsFilePath);
            _fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite;
            _fileSystemWatcher.Changed += FileSystemWatcher_Changed;
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        public bool Exists()
        {
            return _fileSystem.File.Exists(HostsFilePath);
        }

        public async Task<HostsData> ReadAsync()
        {
            var entries = new List<Entry>();
            var unparsedBuilder = new StringBuilder();
            var splittedEntries = false;

            if (!Exists())
            {
                return new HostsData(entries, unparsedBuilder.ToString(), false);
            }

            var lines = await _fileSystem.File.ReadAllLinesAsync(HostsFilePath, Encoding);

            var id = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var entry = new Entry(id, line);

                if (entry.Valid)
                {
                    entries.Add(entry);
                    id++;
                }
                else if (entry.Validate(false))
                {
                    foreach (var hostsChunk in entry.SplittedHosts.Chunk(Consts.MaxHostsCount))
                    {
                        var clonedEntry = entry.Clone();
                        clonedEntry.Id = id;
                        clonedEntry.Hosts = string.Join(' ', hostsChunk);
                        entries.Add(clonedEntry);
                        id++;
                    }

                    splittedEntries = true;
                }
                else
                {
                    if (unparsedBuilder.Length > 0)
                    {
                        unparsedBuilder.Append(Environment.NewLine);
                    }

                    unparsedBuilder.Append(line);
                }
            }

            return new HostsData(entries, unparsedBuilder.ToString(), splittedEntries);
        }

        public async Task WriteAsync(string additionalLines, IEnumerable<Entry> entries)
        {
            if (!_elevationHelper.IsElevated)
            {
                throw new NotRunningElevatedException();
            }

            if (_fileSystem.FileInfo.New(HostsFilePath).IsReadOnly)
            {
                throw new ReadOnlyHostsException();
            }

            var lines = new List<string>();

            if (entries.Any())
            {
                var addressPadding = entries.Max(e => e.Address.Length) + 1;
                var hostsPadding = entries.Max(e => e.Hosts.Length) + 1;
                var anyDisabled = entries.Any(e => !e.Active);

                foreach (var e in entries)
                {
                    var lineBuilder = new StringBuilder();

                    if (!e.Valid)
                    {
                        lineBuilder.Append(e.Line);
                    }
                    else
                    {
                        if (!e.Active)
                        {
                            lineBuilder.Append('#').Append(' ');
                        }
                        else if (anyDisabled)
                        {
                            lineBuilder.Append(' ').Append(' ');
                        }

                        lineBuilder.Append(e.Address.PadRight(addressPadding));
                        lineBuilder.Append(string.Join(' ', e.Hosts).PadRight(hostsPadding));

                        if (e.Comment != string.Empty)
                        {
                            lineBuilder.Append('#').Append(' ');
                            lineBuilder.Append(e.Comment);
                        }

                        lines.Add(lineBuilder.ToString().TrimEnd());
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(additionalLines))
            {
                if (_userSettings.AdditionalLinesPosition == HostsAdditionalLinesPosition.Top)
                {
                    lines.Insert(0, additionalLines);
                }
                else if (_userSettings.AdditionalLinesPosition == HostsAdditionalLinesPosition.Bottom)
                {
                    lines.Add(additionalLines);
                }
            }

            try
            {
                await _asyncLock.WaitAsync();
                _fileSystemWatcher.EnableRaisingEvents = false;

                if (!_backupDone && Exists())
                {
                    _fileSystem.File.Copy(HostsFilePath, HostsFilePath + _backupSuffix + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture));
                    _backupDone = true;
                }

                // FileMode.OpenOrCreate is necessary to prevent UnauthorizedAccessException when the hosts file is hidden
                using var stream = _fileSystem.FileStream.New(HostsFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, _defaultBufferSize, FileOptions.Asynchronous);
                using var writer = new StreamWriter(stream, Encoding);
                foreach (var line in lines)
                {
                    await writer.WriteLineAsync(line.AsMemory());
                }

                stream.SetLength(stream.Position);
                await writer.FlushAsync();
            }
            finally
            {
                _fileSystemWatcher.EnableRaisingEvents = true;
                _asyncLock.Release();
            }
        }

        public async Task<bool> PingAsync(string address)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(address, 4000); // 4000 is the default ping timeout for ping.exe
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        public void CleanupBackup()
        {
            Directory.GetFiles(Path.GetDirectoryName(HostsFilePath), $"*{_backupSuffix}*")
                .Select(f => new FileInfo(f))
                .Where(f => f.CreationTime < DateTime.Now.AddDays(-15))
                .ToList()
                .ForEach(f => f.Delete());
        }

        public void OpenHostsFile()
        {
            var notepadFallback = false;

            try
            {
                // Try to open in default editor
                var key = Registry.ClassesRoot.OpenSubKey("SystemFileAssociations\\text\\shell\\edit\\command");
                if (key != null)
                {
                    var commandPattern = key.GetValue(string.Empty).ToString(); // Default value
                    var file = null as string;
                    var args = null as string;

                    if (commandPattern.StartsWith('\"'))
                    {
                        var endQuoteIndex = commandPattern.IndexOf('\"', 1);
                        if (endQuoteIndex != -1)
                        {
                            file = commandPattern[1..endQuoteIndex];
                            args = commandPattern[(endQuoteIndex + 1)..].Trim();
                        }
                    }
                    else
                    {
                        var spaceIndex = commandPattern.IndexOf(' ');
                        if (spaceIndex != -1)
                        {
                            file = commandPattern[..spaceIndex];
                            args = commandPattern[(spaceIndex + 1)..].Trim();
                        }
                    }

                    if (file != null && args != null)
                    {
                        args = args.Replace("%1", HostsFilePath);
                        Process.Start(new ProcessStartInfo(file, args));
                    }
                    else
                    {
                        notepadFallback = true;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Logger.LogError("Failed to open default editor", ex);
                notepadFallback = true;
            }

            if (notepadFallback)
            {
                try
                {
                    Process.Start(new ProcessStartInfo("notepad.exe", HostsFilePath));
                }
                catch (Exception ex)
                {
                    LoggerInstance.Logger.LogError("Failed to open notepad", ex);
                }
            }
        }

        public void RemoveReadOnlyAttribute()
        {
            var fileInfo = _fileSystem.FileInfo.New(HostsFilePath);
            if (fileInfo.IsReadOnly)
            {
                fileInfo.IsReadOnly = false;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            _fileSystemWatcher.EnableRaisingEvents = false;
            FileChanged?.Invoke(this, EventArgs.Empty);
            _fileSystemWatcher.EnableRaisingEvents = true;
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
    }
}
