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
using Hosts.Models;
using Hosts.Settings;
using Microsoft.Win32;
using Settings.UI.Library.Enumerations;

namespace Hosts.Helpers
{
    public class HostsService : IHostsService, IDisposable
    {
        private const string _backupSuffix = $"_PowerToysBackup_";

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

        public HostsService(
            IFileSystem fileSystem,
            IUserSettings userSettings,
            IElevationHelper elevationHelper)
        {
            _fileSystem = fileSystem;
            _userSettings = userSettings;
            _elevationHelper = elevationHelper;

            _hostsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\drivers\etc\hosts");

            _fileSystemWatcher = _fileSystem.FileSystemWatcher.CreateNew();
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

        public async Task<(string Unparsed, List<Entry> Entries)> ReadAsync()
        {
            var entries = new List<Entry>();
            var unparsedBuilder = new StringBuilder();

            if (!Exists())
            {
                return (unparsedBuilder.ToString(), entries);
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
                    entries.Add(entry);
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

            return (unparsedBuilder.ToString(), entries);
        }

        public async Task<bool> WriteAsync(string additionalLines, IEnumerable<Entry> entries)
        {
            if (!_elevationHelper.IsElevated)
            {
                return false;
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
                        lineBuilder.Append(e.GetLine());
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
                if (_userSettings.AdditionalLinesPosition == AdditionalLinesPosition.Top)
                {
                    lines.Insert(0, additionalLines);
                }
                else if (_userSettings.AdditionalLinesPosition == AdditionalLinesPosition.Bottom)
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

                await _fileSystem.File.WriteAllLinesAsync(HostsFilePath, lines);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to write hosts file", ex);
                return false;
            }
            finally
            {
                _fileSystemWatcher.EnableRaisingEvents = true;
                _asyncLock.Release();
            }

            return true;
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
                Logger.LogError("Failed to open default editor", ex);
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
                    Logger.LogError("Failed to open notepad", ex);
                }
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
