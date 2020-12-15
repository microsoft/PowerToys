// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class SettingsRepositoryTest
    {
        private static Task<SettingsRepository<GeneralSettings>> GetSettingsRepository(ISettingsUtils settingsUtils)
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
    }
}
