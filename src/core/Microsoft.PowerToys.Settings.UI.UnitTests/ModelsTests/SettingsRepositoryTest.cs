// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class SettingsRepositoryTest
    {
        private Task<SettingsRepository<GeneralSettings>> GetSettingsRepository()
        {
            return Task.Run(() =>
            {
                return SettingsRepository<GeneralSettings>.Instance;
            });
        }


        [TestMethod]
        public void SettingsRepositoryInstanceWhenCalledMustReturnSameObject()
        {
            // The singleton class Settings Repository must always have a single instance

            // Arrange and Act
            SettingsRepository<GeneralSettings> firstInstance = SettingsRepository<GeneralSettings>.Instance;
            SettingsRepository<GeneralSettings> secondInstance = SettingsRepository<GeneralSettings>.Instance;

            // Assert
            Assert.IsTrue(object.ReferenceEquals(firstInstance, secondInstance));
        }

        [TestMethod]
        public void SettingsRepositoryInstanceMustBeTheSameAcrossThreads()
        {
            // Multiple tasks try to access and initialize the settings repository class, however they must all access the same settings Repository object.

            // Arrange
            List<Task<SettingsRepository<GeneralSettings>>> settingsRepoTasks = new List<Task<SettingsRepository<GeneralSettings>>>();
            int numberOfTasks = 100;

            for(int i = 0; i < numberOfTasks; i++)
            {
                settingsRepoTasks.Add(GetSettingsRepository());
            }

            // Act
            Task.WaitAll(settingsRepoTasks.ToArray());

            // Assert
            for(int i=0; i< numberOfTasks-1; i++)
            {
                Assert.IsTrue(object.ReferenceEquals(settingsRepoTasks[i].Result, settingsRepoTasks[i + 1].Result));
            }

        }
    }
}
