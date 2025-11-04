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

        public void Create(string hostsFilePath)
        {
            if (_backupDone || !_userSettings.BackupHosts || !_fileSystem.File.Exists(hostsFilePath))
            {
                return;
            }

            try
            {
                if (!_fileSystem.Directory.Exists(_userSettings.BackupPath))
                {
                    _fileSystem.Directory.CreateDirectory(_userSettings.BackupPath);
                }

                var backupPath = _fileSystem.Path.Combine(_userSettings.BackupPath, $"hosts{BackupSuffix}{DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}");

                _fileSystem.File.Copy(hostsFilePath, backupPath);
                _backupDone = true;
            }
            catch (Exception ex)
            {
                LoggerInstance.Logger.LogError("Backup failed", ex);
            }
        }

        public void Delete()
        {
            switch (_userSettings.DeleteBackupsMode)
            {
                case HostsDeleteBackupMode.Count:
                    DeleteByCount(_userSettings.DeleteBackupsCount);
                    break;
                case HostsDeleteBackupMode.Age:
                    DeleteByAge(_userSettings.DeleteBackupsDays, _userSettings.DeleteBackupsCount);
                    break;
            }
        }

        public void DeleteByCount(int count)
        {
            if (count < 1)
            {
                return;
            }

            var backups = GetAll().OrderByDescending(f => f.CreationTime).Skip(count).ToArray();
            DeleteAll(backups);
        }

        public void DeleteByAge(int days, int count)
        {
            if (days < 1)
            {
                return;
            }

            var backupsEnumerable = GetAll();

            if (count > 0)
            {
                backupsEnumerable = backupsEnumerable.OrderByDescending(f => f.CreationTime).Skip(count);
            }

            var backups = backupsEnumerable.Where(f => f.CreationTime < DateTime.Now.AddDays(-days)).ToArray();
            DeleteAll(backups);
        }

        private IEnumerable<IFileInfo> GetAll()
        {
            if (!_fileSystem.Directory.Exists(_userSettings.BackupPath))
            {
                return [];
            }

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
