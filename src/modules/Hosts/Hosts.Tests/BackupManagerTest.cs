// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        public void Hosts_Backup_Not_Done()
        {
            var fileSystem = new MockFileSystem();
            SetupFiles(fileSystem, false);
            var userSettings = new Mock<IUserSettings>();
            userSettings.Setup(m => m.BackupHosts).Returns(false);
            userSettings.Setup(m => m.BackupPath).Returns(BackupPath);
            var backupManager = new BackupManager(fileSystem, userSettings.Object);
            backupManager.CreateBackup(HostsPath);

            Assert.AreEqual(0, fileSystem.Directory.GetFiles(BackupPath, BackupSearchPattern).Length);
        }

        [TestMethod]
        public void Hosts_Backup_Done_Once()
        {
            var fileSystem = new MockFileSystem();
            SetupFiles(fileSystem, false);
            var userSettings = new Mock<IUserSettings>();
            userSettings.Setup(m => m.BackupHosts).Returns(true);
            userSettings.Setup(m => m.BackupPath).Returns(BackupPath);
            var backupManager = new BackupManager(fileSystem, userSettings.Object);
            backupManager.CreateBackup(HostsPath);
            backupManager.CreateBackup(HostsPath);

            Assert.AreEqual(1, fileSystem.Directory.GetFiles(BackupPath, BackupSearchPattern).Length);
            var hostsContent = fileSystem.File.ReadAllText(HostsPath);
            var backupContent = fileSystem.File.ReadAllText(fileSystem.Directory.GetFiles(BackupPath, BackupSearchPattern)[0]);
            Assert.AreEqual(hostsContent, backupContent);
        }

        [DataTestMethod]
        [DataRow(false, 1, 1)]
        [DataRow(true, 0, 0)]
        [DataRow(true, -1, -1)]
        public void Hosts_Backup_Not_Deleted(bool deleteBackup, int daysToKeep, int copiesToKeep)
        {
            var fileSystem = new MockFileSystem();
            SetupFiles(fileSystem, true);
            var userSettings = new Mock<IUserSettings>();
            userSettings.Setup(m => m.BackupPath).Returns(BackupPath);
            userSettings.Setup(m => m.DeleteBackups).Returns(deleteBackup);
            userSettings.Setup(m => m.DaysToKeep).Returns(daysToKeep);
            userSettings.Setup(m => m.CopiesToKeep).Returns(copiesToKeep);
            var backupManager = new BackupManager(fileSystem, userSettings.Object);
            backupManager.DeleteBackups();

            Assert.AreEqual(10, fileSystem.Directory.GetFiles(BackupPath, BackupSearchPattern).Length);
        }

        // MockFileSystem doesn't support CreationTime, so we can't test the DaysToKeep logic
        [DataTestMethod]
        [DataRow(0, 4, 4)]
        [DataRow(2, 4, 4)]
        public void Hosts_Backup_Deleted(int daysToKeep, int copiesToKeep, int expectedBackups)
        {
            var fileSystem = new MockFileSystem();
            SetupFiles(fileSystem, true);
            var userSettings = new Mock<IUserSettings>();
            userSettings.Setup(m => m.BackupPath).Returns(BackupPath);
            userSettings.Setup(m => m.DeleteBackups).Returns(true);
            userSettings.Setup(m => m.DaysToKeep).Returns(daysToKeep);
            userSettings.Setup(m => m.CopiesToKeep).Returns(copiesToKeep);
            var backupManager = new BackupManager(fileSystem, userSettings.Object);
            backupManager.DeleteBackups();

            Assert.AreEqual(expectedBackups + 1, fileSystem.Directory.GetFiles(BackupPath).Length);
            Assert.AreEqual(expectedBackups, fileSystem.Directory.GetFiles(BackupPath, BackupSearchPattern).Length);
        }

        private void SetupFiles(MockFileSystem fileSystem, bool addBackups)
        {
            fileSystem.AddFile(HostsPath, new MockFileData("HOSTS FILE CONTENT"));
            fileSystem.AddFile(fileSystem.Path.Combine(BackupPath, "unrelated_file"), new MockFileData(string.Empty));

            if (addBackups)
            {
                for (var i = 0; i < 10; i++)
                {
                    fileSystem.AddEmptyFile(fileSystem.Path.Combine(BackupPath, $"hosts_PowerToysBackup_{i}"));
                }
            }
        }
    }
}
