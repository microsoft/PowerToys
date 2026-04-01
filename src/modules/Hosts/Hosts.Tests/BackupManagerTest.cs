// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions.TestingHelpers;
using HostsUILib.Helpers;
using HostsUILib.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Hosts.Tests
{
    [TestClass]
    public class BackupManagerTest
    {
        private const string HostsPath = @"C:\Windows\System32\Drivers\etc\hosts";
        private const string BackupPath = @"C:\Backup\hosts";
        private const string BackupSearchPattern = $"*_PowerToysBackup_*";

        [TestMethod]
        public void Hosts_Backup_Not_Executed()
        {
            var fileSystem = new MockFileSystem();
            SetupFiles(fileSystem, true);
            var userSettings = new Mock<IUserSettings>();
            userSettings.Setup(m => m.BackupHosts).Returns(false);
            userSettings.Setup(m => m.BackupPath).Returns(BackupPath);
            var backupManager = new BackupManager(fileSystem, userSettings.Object);
            backupManager.Create(HostsPath);

            Assert.AreEqual(0, fileSystem.Directory.GetFiles(BackupPath, BackupSearchPattern).Length);
        }

        [TestMethod]
        public void Hosts_Backup_Executed_Once()
        {
            var fileSystem = new MockFileSystem();
            SetupFiles(fileSystem, true);
            var userSettings = new Mock<IUserSettings>();
            userSettings.Setup(m => m.BackupHosts).Returns(true);
            userSettings.Setup(m => m.BackupPath).Returns(BackupPath);
            var backupManager = new BackupManager(fileSystem, userSettings.Object);
            backupManager.Create(HostsPath);
            backupManager.Create(HostsPath);

            Assert.AreEqual(1, fileSystem.Directory.GetFiles(BackupPath, BackupSearchPattern).Length);
            var hostsContent = fileSystem.File.ReadAllText(HostsPath);
            var backupContent = fileSystem.File.ReadAllText(fileSystem.Directory.GetFiles(BackupPath, BackupSearchPattern)[0]);
            Assert.AreEqual(hostsContent, backupContent);
        }

        [DataTestMethod]
        [DataRow(-10, -10)]
        [DataRow(-10, 0)]
        [DataRow(-10, 10)]
        [DataRow(0, -10)]
        [DataRow(0, 0)]
        [DataRow(0, 10)]
        [DataRow(10, -10)]
        [DataRow(10, 0)]
        [DataRow(10, 10)]
        public void Hosts_Backups_Delete_Never(int count, int days)
        {
            var fileSystem = new MockFileSystem();
            SetupFiles(fileSystem, false);
            var userSettings = new Mock<IUserSettings>();
            userSettings.Setup(m => m.BackupPath).Returns(BackupPath);
            userSettings.Setup(m => m.DeleteBackupsMode).Returns(HostsDeleteBackupMode.Never);
            var backupManager = new BackupManager(fileSystem, userSettings.Object);
            backupManager.Delete();

            Assert.AreEqual(30, fileSystem.Directory.GetFiles(BackupPath, BackupSearchPattern).Length);
            Assert.AreEqual(31, fileSystem.Directory.GetFiles(BackupPath).Length);
        }

        [DataTestMethod]
        [DataRow(-10, 30)]
        [DataRow(0, 30)]
        [DataRow(10, 10)]
        public void Hosts_Backups_Delete_ByCount(int count, int expectedBackups)
        {
            var fileSystem = new MockFileSystem();
            SetupFiles(fileSystem, false);
            var userSettings = new Mock<IUserSettings>();
            userSettings.Setup(m => m.BackupPath).Returns(BackupPath);
            userSettings.Setup(m => m.DeleteBackupsMode).Returns(HostsDeleteBackupMode.Count);
            userSettings.Setup(m => m.DeleteBackupsCount).Returns(count);
            var backupManager = new BackupManager(fileSystem, userSettings.Object);
            backupManager.Delete();

            Assert.AreEqual(expectedBackups, fileSystem.Directory.GetFiles(BackupPath, BackupSearchPattern).Length);
            Assert.AreEqual(expectedBackups + 1, fileSystem.Directory.GetFiles(BackupPath).Length);
        }

        [DataTestMethod]
        [DataRow(-10, -10, 30)]
        [DataRow(-10, 0, 30)]
        [DataRow(-10, 10, 5)]
        [DataRow(0, -10, 30)]
        [DataRow(0, 0, 30)]
        [DataRow(0, 10, 5)]
        [DataRow(10, -10, 30)]
        [DataRow(10, 0, 30)]
        [DataRow(5, 1, 5)]
        [DataRow(1, 15, 10)]
        [DataRow(2, 2, 2)]
        public void Hosts_Backups_Delete_ByAge(int count, int days, int expectedBackups)
        {
            var fileSystem = new MockFileSystem();
            SetupFiles(fileSystem, false);
            var userSettings = new Mock<IUserSettings>();
            userSettings.Setup(m => m.BackupPath).Returns(BackupPath);
            userSettings.Setup(m => m.DeleteBackupsMode).Returns(HostsDeleteBackupMode.Age);
            userSettings.Setup(m => m.DeleteBackupsCount).Returns(count);
            userSettings.Setup(m => m.DeleteBackupsDays).Returns(days);
            var backupManager = new BackupManager(fileSystem, userSettings.Object);
            backupManager.Delete();

            Assert.AreEqual(expectedBackups, fileSystem.Directory.GetFiles(BackupPath, BackupSearchPattern).Length);
            Assert.AreEqual(expectedBackups + 1, fileSystem.Directory.GetFiles(BackupPath).Length);
        }

        private void SetupFiles(MockFileSystem fileSystem, bool hostsOnly)
        {
            fileSystem.AddDirectory(BackupPath);
            fileSystem.AddFile(HostsPath, new MockFileData("HOSTS FILE CONTENT"));

            if (hostsOnly)
            {
                return;
            }

            var today = new DateTimeOffset(DateTime.Today);

            var notBackupData = new MockFileData("NOT A BACKUP")
            {
                CreationTime = today.AddDays(-100),
            };

            fileSystem.AddFile(fileSystem.Path.Combine(BackupPath, "hosts_not_a_backup"), notBackupData);

            // The first backup is from 5 days ago. There are 30 backups, one for each day.
            var offset = 5;
            for (var i = 0; i < 30; i++)
            {
                var backupData = new MockFileData("THIS IS A BACKUP")
                {
                    CreationTime = today.AddDays(-i - offset),
                };

                fileSystem.AddFile(fileSystem.Path.Combine(BackupPath, $"hosts_PowerToysBackup_{i}"), backupData);
            }
        }
    }
}
