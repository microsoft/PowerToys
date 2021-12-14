// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Moq;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.Mocks
{
    internal static class ISettingsUtilsMocks
    {
        // Stubs out empty values for imageresizersettings and general settings as needed by the imageresizer viewmodel
        internal static Mock<ISettingsUtils> GetStubSettingsUtils<T>()
            where T : ISettingsConfig, new()
        {
            var settingsUtils = new Mock<ISettingsUtils>();
            settingsUtils
                .Setup(x => x.GetSettingsOrDefault<T>(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new T());

            settingsUtils
                .Setup(x => x.GetSettings<T>(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new T());

            return settingsUtils;
        }
    }
}
