// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.UnitTests;
using Microsoft.PowerToys.Settings.UnitTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class SettingsUtilsTests
    {
        private const string ModuleName = "SettingsUtilsTests";
        private const string OldSettings = "{\"name\":\"old name\",\"version\":\"1.0\"}";
        private const string NewSettings = "{\"name\":\"new name\",\"version\":\"2.0\"}";
        private const string UnreadableSettings = "{\"name\":\"my module\",\"version\":";

        [TestMethod]
        public void SaveSettingsSaveSettingsToFileWhenFilePathExists()
        {
            // Arrange
            var mockFileSystem = new MockFileSystem();
            var testSerializerOptions = new JsonSerializerOptions
            {
                MaxDepth = 0,
                IncludeFields = true,
                TypeInfoResolver = TestSettingsSerializationContext.Default,
            };
            var settingsUtils = new SettingsUtils(mockFileSystem, testSerializerOptions);

            string file_name = "\\test";
            string file_contents_correct_json_content = "{\"name\":\"powertoy module name\",\"version\":\"powertoy version\"}";

            BasePTSettingsTest expected_json = JsonSerializer.Deserialize<BasePTSettingsTest>(file_contents_correct_json_content);

            // Act
            settingsUtils.SaveSettings(file_contents_correct_json_content, file_name);
            BasePTSettingsTest actual_json = settingsUtils.GetSettingsOrDefault<BasePTSettingsTest>(file_name);

            // Assert
            Assert.AreEqual(expected_json.ToJsonString(), actual_json.ToJsonString());
        }

        [TestMethod]
        public void SaveSettingsShouldCreateFileWhenFilePathIsNotFound()
        {
            // Arrange
            var mockFileSystem = new MockFileSystem();
            var testSerializerOptions = new JsonSerializerOptions
            {
                MaxDepth = 0,
                IncludeFields = true,
                TypeInfoResolver = TestSettingsSerializationContext.Default,
            };
            var settingsUtils = new SettingsUtils(mockFileSystem, testSerializerOptions);
            string file_name = "test\\Test Folder";
            string file_contents_correct_json_content = "{\"name\":\"powertoy module name\",\"version\":\"powertoy version\"}";

            BasePTSettingsTest expected_json = JsonSerializer.Deserialize<BasePTSettingsTest>(file_contents_correct_json_content);

            settingsUtils.SaveSettings(file_contents_correct_json_content, file_name);
            BasePTSettingsTest actual_json = settingsUtils.GetSettingsOrDefault<BasePTSettingsTest>(file_name);

            // Assert
            Assert.AreEqual(expected_json.ToJsonString(), actual_json.ToJsonString());
        }

        [TestMethod]
        public void SettingsFolderExistsShouldReturnFalseWhenFilePathIsNotFound()
        {
            // Arrange
            var mockFileSystem = new MockFileSystem();
            var settingsUtils = new SettingsUtils(mockFileSystem);
            string file_name_random = "test\\" + RandomString();
            string file_name_exists = "test\\exists";
            string file_contents_correct_json_content = "{\"name\":\"powertoy module name\",\"version\":\"powertoy version\"}";

            // Act
            bool pathNotFound = settingsUtils.SettingsExists(file_name_random);

            settingsUtils.SaveSettings(file_contents_correct_json_content, file_name_exists);
            bool pathFound = settingsUtils.SettingsExists(file_name_exists);

            // Assert
            Assert.IsFalse(pathNotFound);
            Assert.IsTrue(pathFound);
        }

        [TestMethod]
        public void SettingsUtilsMustReturnDefaultItemWhenFileIsCorrupt()
        {
            // Arrange
            var mockFileSystem = new MockFileSystem();
            var mockSettingsUtils = new SettingsUtils(mockFileSystem);

            // Act
            TestClass settings = mockSettingsUtils.GetSettingsOrDefault<TestClass>(string.Empty);

            // Assert
            Assert.AreEqual(100, settings.TestInt);
            Assert.AreEqual("test", settings.TestString);
        }

        [TestMethod]
        public void SaveSettingsKeepsThePreviousSettingsWhenTheDiskIsFull()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var file = new FaultyFile(fileSystem);
            var settingsUtils = CreateSettingsUtils(fileSystem, file);
            string settingsPath = settingsUtils.GetSettingsFilePath(ModuleName);
            fileSystem.AddFile(settingsPath, new MockFileData(OldSettings));

            // Act
            file.DiskIsFull = true;
            settingsUtils.SaveSettings(NewSettings, ModuleName);

            // Assert
            Assert.AreEqual(OldSettings, fileSystem.File.ReadAllText(settingsPath));
            CollectionAssert.AreEqual(new[] { settingsPath }, fileSystem.Directory.GetFiles(fileSystem.Path.GetDirectoryName(settingsPath)));
        }

        [TestMethod]
        public void SaveSettingsOrThrowThrowsAndKeepsThePreviousSettingsWhenTheDiskIsFull()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var file = new FaultyFile(fileSystem);
            var settingsUtils = CreateSettingsUtils(fileSystem, file);
            string settingsPath = settingsUtils.GetSettingsFilePath(ModuleName);
            fileSystem.AddFile(settingsPath, new MockFileData(OldSettings));

            // Act
            file.DiskIsFull = true;
            Assert.ThrowsExactly<IOException>(() => settingsUtils.SaveSettingsOrThrow(NewSettings, ModuleName));

            // Assert
            Assert.AreEqual(OldSettings, fileSystem.File.ReadAllText(settingsPath));
            CollectionAssert.AreEqual(new[] { settingsPath }, fileSystem.Directory.GetFiles(fileSystem.Path.GetDirectoryName(settingsPath)));
        }

        [TestMethod]
        public void SaveSettingsSavesEvenIfAnotherProcessBrieflyHasTheFileOpen()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var file = new FaultyFile(fileSystem);
            var settingsUtils = CreateSettingsUtils(fileSystem, file);
            string settingsPath = settingsUtils.GetSettingsFilePath(ModuleName);
            fileSystem.AddFile(settingsPath, new MockFileData(OldSettings));

            // Act
            file.TimesTheFileIsInUse = 2;
            settingsUtils.SaveSettings(NewSettings, ModuleName);

            // Assert
            Assert.AreEqual(NewSettings, fileSystem.File.ReadAllText(settingsPath));
        }

        [TestMethod]
        public void SaveSettingsChangesTheLastWriteTimeOfTheSettingsFile()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var file = new FaultyFile(fileSystem);
            var settingsUtils = CreateSettingsUtils(fileSystem, file);
            string settingsPath = settingsUtils.GetSettingsFilePath(ModuleName);
            fileSystem.AddFile(settingsPath, new MockFileData(OldSettings));

            // Act
            settingsUtils.SaveSettings(NewSettings, ModuleName);

            // Assert
            // The settings file watchers only listen for a change of the last write time of the settings file itself.
            Assert.AreEqual(settingsPath, file.LastWrittenPath);
        }

        [TestMethod]
        public void SaveSettingsWritesThroughASymbolicLink()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var file = new FaultyFile(fileSystem);
            var settingsUtils = CreateSettingsUtils(fileSystem, file);
            string settingsPath = settingsUtils.GetSettingsFilePath(ModuleName);
            fileSystem.AddFile(settingsPath, new MockFileData(OldSettings));
            fileSystem.File.SetAttributes(settingsPath, FileAttributes.ReparsePoint);

            // Act
            settingsUtils.SaveSettings(NewSettings, ModuleName);

            // Assert
            Assert.AreEqual(NewSettings, fileSystem.File.ReadAllText(settingsPath));
            CollectionAssert.DoesNotContain(file.ReplacedFiles, settingsPath, "The link was replaced by a regular file.");
        }

        [TestMethod]
        public void SaveSettingsWritesUtf8WithoutAByteOrderMark()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var file = new FaultyFile(fileSystem);
            var settingsUtils = CreateSettingsUtils(fileSystem, file);
            string settingsPath = settingsUtils.GetSettingsFilePath(ModuleName);
            string settings = "{\"name\":\"" + (char)0xE9 + "\"}";

            // Act
            settingsUtils.SaveSettings(settings, ModuleName);

            // Assert
            // File.WriteAllText never wrote a byte order mark, so the file has to keep starting with the JSON itself.
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(settings), fileSystem.File.ReadAllBytes(settingsPath));
        }

        [TestMethod]
        public void GetSettingsOrDefaultReadsSettingsEvenIfAnotherProcessBrieflyHasTheFileOpen()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var file = new FaultyFile(fileSystem);
            var settingsUtils = CreateSettingsUtils(fileSystem, file);
            fileSystem.AddFile(settingsUtils.GetSettingsFilePath(ModuleName), new MockFileData(OldSettings));

            // Act
            file.ReadsThatFail = 2;
            BasePTSettingsTest settings = settingsUtils.GetSettingsOrDefault<BasePTSettingsTest>(ModuleName);

            // Assert
            Assert.AreEqual("old name", settings.Name);
        }

        [TestMethod]
        public void GetSettingsOrDefaultDoesNotResetSettingsWhileTheFileStaysInUse()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var file = new FaultyFile(fileSystem);
            var settingsUtils = CreateSettingsUtils(fileSystem, file);
            string settingsPath = settingsUtils.GetSettingsFilePath(ModuleName);
            fileSystem.AddFile(settingsPath, new MockFileData(OldSettings));

            // Act and assert
            file.ReadsThatFail = int.MaxValue;
            Assert.ThrowsExactly<IOException>(() => settingsUtils.GetSettingsOrDefault<BasePTSettingsTest>(ModuleName));
            Assert.AreEqual(OldSettings, fileSystem.File.ReadAllText(settingsPath));
        }

        [TestMethod]
        public void GetSettingsOrDefaultKeepsACopyOfSettingsItCannotParse()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var file = new FaultyFile(fileSystem);
            var settingsUtils = CreateSettingsUtils(fileSystem, file);
            string settingsPath = settingsUtils.GetSettingsFilePath(ModuleName);
            fileSystem.AddFile(settingsPath, new MockFileData(UnreadableSettings));

            // Act
            BasePTSettingsTest settings = settingsUtils.GetSettingsOrDefault<BasePTSettingsTest>(ModuleName);

            // Assert
            Assert.AreEqual(string.Empty, settings.Name);
            Assert.IsTrue(fileSystem.File.Exists(settingsPath + ".corrupt"), "No copy of the unreadable settings file was kept.");
            Assert.AreEqual(UnreadableSettings, fileSystem.File.ReadAllText(settingsPath + ".corrupt"));
        }

        [TestMethod]
        public void GetSettingsOrDefaultWithAnUpgraderKeepsACopyOfSettingsItCannotParse()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var file = new FaultyFile(fileSystem);
            var settingsUtils = CreateSettingsUtils(fileSystem, file);
            string settingsPath = settingsUtils.GetSettingsFilePath(ModuleName);
            fileSystem.AddFile(settingsPath, new MockFileData(UnreadableSettings));

            // Act
            BasePTSettingsTest settings = settingsUtils.GetSettingsOrDefault<BasePTSettingsTest, BasePTSettingsTest>(ModuleName, SettingsUtils.DefaultFileName, oldSettings => oldSettings);

            // Assert
            Assert.AreEqual(string.Empty, settings.Name);
            Assert.IsTrue(fileSystem.File.Exists(settingsPath + ".corrupt"), "No copy of the unreadable settings file was kept.");
            Assert.AreEqual(UnreadableSettings, fileSystem.File.ReadAllText(settingsPath + ".corrupt"));
        }

        [TestMethod]
        public void GetSettingsOrDefaultKeepsTheEarlierCopyWhenTheSettingsFileHoldsNothing()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var file = new FaultyFile(fileSystem);
            var settingsUtils = CreateSettingsUtils(fileSystem, file);
            string settingsPath = settingsUtils.GetSettingsFilePath(ModuleName);

            // An interrupted write can leave a file that holds nothing but zero bytes.
            fileSystem.AddFile(settingsPath, new MockFileData(new string((char)0, 16)));
            fileSystem.AddFile(settingsPath + ".corrupt", new MockFileData(UnreadableSettings));

            // Act
            settingsUtils.GetSettingsOrDefault<BasePTSettingsTest>(ModuleName);

            // Assert
            Assert.AreEqual(UnreadableSettings, fileSystem.File.ReadAllText(settingsPath + ".corrupt"));
        }

        public static string RandomString()
        {
            Random random = new Random();
            int length = 20;
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private static SettingsUtils CreateSettingsUtils(MockFileSystem fileSystem, FaultyFile file)
        {
            var serializerOptions = new JsonSerializerOptions
            {
                MaxDepth = 0,
                IncludeFields = true,
                TypeInfoResolver = TestSettingsSerializationContext.Default,
            };

            return new SettingsUtils(file, new SettingPath(fileSystem.Directory, fileSystem.Path), serializerOptions);
        }

        private sealed partial class TestClass : ISettingsConfig
        {
            public int TestInt { get; set; } = 100;

            public string TestString { get; set; } = "test";

            public string GetModuleName()
            {
                throw new NotImplementedException();
            }

            public string ToJsonString()
            {
                return JsonSerializer.Serialize(this);
            }

            public bool UpgradeSettingsConfiguration()
            {
                throw new NotImplementedException();
            }
        }

        // The mock file system never fails. This adds the failures of a real disk that the settings have to survive.
        private sealed class FaultyFile : MockFile
        {
            private const int SharingViolation = unchecked((int)0x80070020);

            public FaultyFile(MockFileSystem fileSystem)
                : base(fileSystem)
            {
            }

            // How many of the next attempts to write or replace a file fail because another process has it open.
            public int TimesTheFileIsInUse { get; set; }

            // How many of the next attempts to read a file fail because another process is writing to it.
            public int ReadsThatFail { get; set; }

            // When set, a file can still be created or truncated, but nothing can be written to it.
            public bool DiskIsFull { get; set; }

            // The file whose last write time changed most recently. Renaming a file does not change its last write time.
            public string LastWrittenPath { get; private set; }

            // The files that were replaced by renaming another file over them.
            public List<string> ReplacedFiles { get; } = new List<string>();

            public override void WriteAllText(string path, string contents)
            {
                if (TimesTheFileIsInUse > 0)
                {
                    TimesTheFileIsInUse--;
                    throw new IOException("The process cannot access the file because it is being used by another process.", SharingViolation);
                }

                LastWrittenPath = path;
                if (DiskIsFull)
                {
                    base.WriteAllText(path, string.Empty);
                    throw new IOException("There is not enough space on the disk.");
                }

                base.WriteAllText(path, contents);
            }

            public override FileSystemStream Create(string path)
            {
                FileSystemStream stream = base.Create(path);
                LastWrittenPath = path;
                if (!DiskIsFull)
                {
                    return stream;
                }

                stream.Dispose();
                return new FullDiskStream(path);
            }

            public override void Move(string sourceFileName, string destFileName, bool overwrite)
            {
                if (TimesTheFileIsInUse > 0)
                {
                    TimesTheFileIsInUse--;
                    throw new UnauthorizedAccessException("Access to the path is denied.");
                }

                base.Move(sourceFileName, destFileName, overwrite);
                ReplacedFiles.Add(destFileName);
            }

            public override void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc)
            {
                base.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
                LastWrittenPath = path;
            }

            public override string ReadAllText(string path)
            {
                if (ReadsThatFail > 0)
                {
                    ReadsThatFail--;
                    throw new IOException("The process cannot access the file because it is being used by another process.", SharingViolation);
                }

                return base.ReadAllText(path);
            }

            private sealed class FullDiskStream : FileSystemStream
            {
                public FullDiskStream(string path)
                    : base(new ThrowingStream(), path, false)
                {
                }
            }

            private sealed class ThrowingStream : MemoryStream
            {
                public override void Write(byte[] buffer, int offset, int count)
                {
                    throw new IOException("There is not enough space on the disk.");
                }

                public override void Write(ReadOnlySpan<byte> buffer)
                {
                    throw new IOException("There is not enough space on the disk.");
                }
            }
        }
    }
}
