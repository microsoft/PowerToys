// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
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

            try
            {
                _fileSystem.File.Copy(hostsFilePath, backupPath);
                _backupDone = true;
            }
            catch (Exception ex)
            {
                LoggerInstance.Logger.LogError("Backup failed", ex);
            }
        }

        public void DeleteBackups()
        {
            switch (_userSettings.DeleteBackupsMode)
            {
                case HostsDeleteBackupMode.Count:
                    DeleteBackupsByCount(_userSettings.DeleteBackupsCount);
                    break;
                case HostsDeleteBackupMode.Age:
                    DeleteBackupsByAge(_userSettings.DeleteBackupsDays, _userSettings.DeleteBackupsCount);
                    break;
            }
        }

        public void DeleteBackupsByCount(int count)
        {
            if (count < 1)
            {
                return;
            }

            var backups = GetBackups().OrderByDescending(f => f.CreationTime).Skip(count).ToArray();
            DeleteAll(backups);
        }

        public void DeleteBackupsByAge(int days, int count)
        {
            if (days < 1)
            {
                return;
            }

            var backupsEnumerable = GetBackups();

            if (count > 0)
            {
                backupsEnumerable = backupsEnumerable.OrderByDescending(f => f.CreationTime).Skip(count);
            }

            var backups = backupsEnumerable.Where(f => f.CreationTime < DateTime.Now.AddDays(-days)).ToArray();
            DeleteAll(backups);
        }

        private IEnumerable<IFileInfo> GetBackups()
        {
            return _fileSystem.Directory.GetFiles(_userSettings.BackupPath, $"*{BackupSuffix}*").Select(_fileSystem.FileInfo.New);
        }

        private void DeleteAll(IFileInfo[] files)
        {
            foreach (var f in files)
            {
                _fileSystem.File.Delete(f.FullName);
            }
        }
    }
}
