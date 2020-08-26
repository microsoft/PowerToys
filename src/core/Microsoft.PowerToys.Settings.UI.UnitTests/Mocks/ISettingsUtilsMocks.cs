using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.Mocks
{
    internal static class ISettingsUtilsMocks
    {
        //Stubs out empty values for imageresizersettings and general settings as needed by the imageresizer viewmodel
        internal static Mock<ISettingsUtils> GetStubSettingsUtils()
        {
            var settingsUtils = new Mock<ISettingsUtils>();
            settingsUtils.Setup(x => x.GetSettings<GeneralSettings>(It.IsAny<string>(), It.IsAny<string>())).Returns(new GeneralSettings());
            settingsUtils.Setup(x => x.GetSettings<ImageResizerSettings>(It.IsAny<string>(), It.IsAny<string>())).Returns(new ImageResizerSettings());
            settingsUtils.Setup(x => x.GetSettings<ColorPickerSettings>(It.IsAny<string>(), It.IsAny<string>())).Returns(new ColorPickerSettings(settingsUtils.Object));
            settingsUtils.Setup(x => x.GetSettings<KeyboardManagerSettings>(It.IsAny<string>(), It.IsAny<string>())).Returns(new KeyboardManagerSettings());
            settingsUtils.Setup(x => x.GetSettings<PowerLauncherSettings>(It.IsAny<string>(), It.IsAny<string>())).Returns(new PowerLauncherSettings(settingsUtils.Object));
            settingsUtils.Setup(x => x.GetSettings<PowerPreviewSettings>(It.IsAny<string>(), It.IsAny<string>())).Returns(new PowerPreviewSettings());
            settingsUtils.Setup(x => x.GetSettings<PowerRenameLocalProperties>(It.IsAny<string>(), It.IsAny<string>())).Returns(new PowerRenameLocalProperties());
            settingsUtils.Setup(x => x.GetSettings<GeneralSettings>(It.IsAny<string>(), It.IsAny<string>())).Returns(new GeneralSettings());
            settingsUtils.Setup(x => x.GetSettings<FancyZonesSettings>(It.IsAny<string>(), It.IsAny<string>())).Returns(new FancyZonesSettings());
            settingsUtils.Setup(x => x.GetSettings<ShortcutGuideSettings>(It.IsAny<string>(), It.IsAny<string>())).Returns(new ShortcutGuideSettings());

            return settingsUtils;
        }

    }
}
