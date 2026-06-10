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
        public void NewGrabActionsAreEnabledByDefault()
        {
            var settings = new GrabAndMoveSettings();

            Assert.IsTrue(settings.Properties.UseScreenEdgeMaximize.Value);
            Assert.IsTrue(settings.Properties.UseScreenEdgeSnap.Value);
        }

        [TestMethod]
        public void UpdatingNewGrabActionsNotifiesRunner()
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

            viewModel.UseScreenEdgeMaximize = false;
            viewModel.UseScreenEdgeSnap = false;

            var outgoingSettings = JsonSerializer.Deserialize<SndModuleSettings<SndGrabAndMoveSettings>>(serializedSettings);
            Assert.IsFalse(outgoingSettings!.PowertoysSetting.Settings.Properties.UseScreenEdgeMaximize.Value);
            Assert.IsFalse(outgoingSettings!.PowertoysSetting.Settings.Properties.UseScreenEdgeSnap.Value);
        }
    }
}
