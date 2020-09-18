// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class SettingsRepositoryTest
    {
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
    }
}
