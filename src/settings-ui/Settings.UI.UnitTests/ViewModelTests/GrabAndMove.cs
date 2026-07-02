// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class GrabAndMove
    {
        [TestMethod]
        public void MiddleClickMaximizeIsEnabledByDefault()
        {
            var settings = new GrabAndMoveSettings();

            Assert.IsTrue(settings.Properties.UseMiddleClickMaximize.Value);
        }

        [TestMethod]
        public void UpdatingMiddleClickMaximizeNotifiesRunner()
        {
            var moduleSettings = new GrabAndMoveSettings();
            string serializedSettings = string.Empty;

            using var viewModel = new GrabAndMoveViewModel(
                SettingsRepository<GeneralSettings>.GetInstance(ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>().Object),
                moduleSettings,
                msg =>
                {
                    serializedSettings = msg;
                    return 0;
                });

            viewModel.UseMiddleClickMaximize = false;

            var outgoingSettings = JsonSerializer.Deserialize<SndModuleSettings<SndGrabAndMoveSettings>>(serializedSettings);
            Assert.IsFalse(outgoingSettings!.PowertoysSetting.Settings.Properties.UseMiddleClickMaximize.Value);
        }
    }
}
