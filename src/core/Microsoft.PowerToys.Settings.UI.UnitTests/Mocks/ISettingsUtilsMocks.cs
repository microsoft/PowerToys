using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Moq;
using System;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.Mocks
{
    internal static class ISettingsUtilsMocks
    {
        //Stubs out empty values for imageresizersettings and general settings as needed by the imageresizer viewmodel
        internal static Mock<ISettingsUtils> GetStubSettingsUtils<T>()
            where T : ISettingsConfig, new()
        {
            var settingsUtils = new Mock<ISettingsUtils>();
            settingsUtils.Setup(x => x.GetSettings<T>(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new T());

            return settingsUtils;
        }
    }
}
