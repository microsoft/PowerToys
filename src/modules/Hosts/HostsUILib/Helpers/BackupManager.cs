// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using HostsUILib.Settings;

namespace HostsUILib.Helpers
{
    public class BackupManager : IBackupManager
    {
        private const string BackupSuffix = "_PowerToysBackup_";
        private readonly IFileSystem _fileSystem;
        private readonly IUserSettings _userSettings;
        private bool _backupDone;

        public BackupManager(IFileSystem fileSystem, IUserSettings userSettings)
        {
            _fileSystem = fileSystem;
            _userSettings = userSettings;
        }

        public void CreateBackup(string hostsFilePath)
        {
            if (_backupDone || !_userSettings.BackupHosts || !_fileSystem.File.Exists(hostsFilePath))
            {
                return;
            }

            if (!_fileSystem.Directory.Exists(_userSettings.BackupPath))
            {
                _fileSystem.Directory.CreateDirectory(_userSettings.BackupPath);
            }

            var backupPath = _fileSystem.Path.Combine(_userSettings.BackupPath, $"hosts{BackupSuffix}{DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}");
            _fileSystem.File.Copy(hostsFilePath, backupPath);
            _backupDone = true;
        }

        public void DeleteBackups()
        {
            if (!_userSettings.DeleteBackups || (_userSettings.DaysToKeep <= 0 && _userSettings.CopiesToKeep <= 0))
            {
                return;
            }

            var files = _fileSystem.Directory.GetFiles(_userSettings.BackupPath, $"*{BackupSuffix}*").Select(f => new FileInfo(f));

            if (_userSettings.CopiesToKeep > 0)
            {
                files = files.OrderByDescending(f => f.CreationTime).Skip(_userSettings.CopiesToKeep);
            }

            if (_userSettings.DaysToKeep > 0)
            {
                files = files.Where(f => f.CreationTime < DateTime.Now.AddDays(-_userSettings.DaysToKeep));
            }

            foreach (var f in files)
            {
                _fileSystem.File.Delete(f.FullName);
            }
        }
    }
}
