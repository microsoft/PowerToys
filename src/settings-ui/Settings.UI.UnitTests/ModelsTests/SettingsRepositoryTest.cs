// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.UnitTests;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CommonLibTest
{
    [TestClass]
    public class SettingsRepositoryTest
    {
        private static Task<SettingsRepository<GeneralSettings>> GetSettingsRepository(SettingsUtils settingsUtils)
        {
            return Task.Run(() =>
            {
                return SettingsRepository<GeneralSettings>.GetInstance(settingsUtils);
            });
        }

        [TestMethod]
        public void SettingsRepositoryInstanceWhenCalledMustReturnSameObject()
        {
            // The singleton class Settings Repository must always have a single instance
            var mockSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();

            // Arrange and Act
            SettingsRepository<GeneralSettings> firstInstance = SettingsRepository<GeneralSettings>.GetInstance(mockSettingsUtils.Object);
            SettingsRepository<GeneralSettings> secondInstance = SettingsRepository<GeneralSettings>.GetInstance(mockSettingsUtils.Object);

            // Assert
            Assert.IsTrue(object.ReferenceEquals(firstInstance, secondInstance));
        }

        [TestMethod]
        public void SettingsRepositoryInstanceMustBeTheSameAcrossThreads()
        {
            // Multiple tasks try to access and initialize the settings repository class, however they must all access the same settings Repository object.

            // Arrange
            var mockSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            List<Task<SettingsRepository<GeneralSettings>>> settingsRepoTasks = new List<Task<SettingsRepository<GeneralSettings>>>();
            int numberOfTasks = 100;

            for (int i = 0; i < numberOfTasks; i++)
            {
                settingsRepoTasks.Add(GetSettingsRepository(mockSettingsUtils.Object));
            }

            // Act
            Task.WaitAll(settingsRepoTasks.ToArray());

            // Assert
            for (int i = 0; i < numberOfTasks - 1; i++)
            {
                Assert.IsTrue(object.ReferenceEquals(settingsRepoTasks[i].Result, settingsRepoTasks[i + 1].Result));
            }
        }

        [TestMethod]
        public async Task GeneralSettingsRepositoryReloadsAfterAtomicCreationAndReplacement()
        {
            var directory = Directory.CreateTempSubdirectory("PowerToys-SettingsWatcher-");
            SettingsRepository<WatchedSettings> repository = null;

            try
            {
                var filePath = Path.Combine(directory.FullName, SettingsUtils.DefaultFileName);
                var temporaryPath = Path.Combine(directory.FullName, "settings.tmp");
                var fileSystem = new FileSystem();
                var settingsPath = new Mock<IPath>();
                settingsPath.Setup(path => path.Combine(It.IsAny<string>(), It.IsAny<string>())).Returns(filePath);
                var settingsUtils = new SettingsUtils(
                    fileSystem.File,
                    new SettingPath(fileSystem.Directory, settingsPath.Object),
                    new JsonSerializerOptions { TypeInfoResolver = TestSettingsSerializationContext.Default });

                // A separate settings type keeps the singleton isolated from other tests.
                repository = SettingsRepository<WatchedSettings>.GetInstance(settingsUtils);
                repository.SettingsConfig = new WatchedSettings { Value = 0 };

                await AssertReloadAfterPublish(repository, 1, () =>
                {
                    File.WriteAllText(temporaryPath, new WatchedSettings { Value = 1 }.ToJsonString());
                    File.Move(temporaryPath, filePath);
                });

                await AssertReloadAfterPublish(repository, 2, () =>
                {
                    File.WriteAllText(temporaryPath, new WatchedSettings { Value = 2 }.ToJsonString());
                    File.Move(temporaryPath, filePath, overwrite: true);
                });

                await AssertReloadAfterPublish(repository, 3, () =>
                {
                    File.WriteAllText(temporaryPath, new WatchedSettings { Value = 3 }.ToJsonString());
                    File.Replace(temporaryPath, filePath, null);
                });
            }
            finally
            {
                repository?.Dispose();
                directory.Delete(recursive: true);
            }
        }

        private static async Task AssertReloadAfterPublish(SettingsRepository<WatchedSettings> repository, int expectedValue, Action publish)
        {
            var changed = new TaskCompletionSource<WatchedSettings>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnSettingsChanged(WatchedSettings settings)
            {
                if (settings.Value == expectedValue)
                {
                    changed.TrySetResult(settings);
                }
            }

            repository.SettingsChanged += OnSettingsChanged;
            try
            {
                publish();
                var settings = await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.AreEqual(expectedValue, settings.Value);
                Assert.AreEqual(expectedValue, repository.SettingsConfig.Value);
            }
            finally
            {
                repository.SettingsChanged -= OnSettingsChanged;
            }
        }

        public sealed class WatchedSettings : ISettingsConfig
        {
            public int Value { get; set; }

            public string GetModuleName() => string.Empty;

            public bool UpgradeSettingsConfiguration() => false;

            public string ToJsonString() => JsonSerializer.Serialize(this, TestSettingsSerializationContext.Default.WatchedSettings);
        }
    }
}
