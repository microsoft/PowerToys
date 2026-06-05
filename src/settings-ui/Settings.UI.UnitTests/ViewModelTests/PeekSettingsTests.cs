// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class PeekSettingsTests
    {
        [TestMethod]
        public void JsonRoundTrip_PreservesAllFields()
        {
            var settings = new PeekSettings
            {
                Name = PeekSettings.ModuleName,
                Version = PeekSettings.CurrentModuleVersion,
                Properties = new PeekProperties
                {
                    ActivationShortcut = new HotkeySettings(true, false, true, true, 0x50) { Key = "P" },
                    AlwaysRunNotElevated = new BoolProperty(false),
                    CloseAfterLosingFocus = new BoolProperty(true),
                    ConfirmFileDelete = new BoolProperty(false),
                    EnableSpaceToActivate = new BoolProperty(false),
                    ShowFilePreviewTooltip = new BoolProperty(false),
                },
            };

            var json = settings.ToJsonString();

            var roundTripped = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.PeekSettings);

            Assert.IsNotNull(roundTripped);
            Assert.AreEqual(settings.Name, roundTripped.Name);
            Assert.AreEqual(settings.Version, roundTripped.Version);
            Assert.AreEqual(settings.Properties.ActivationShortcut.Win, roundTripped.Properties.ActivationShortcut.Win);
            Assert.AreEqual(settings.Properties.ActivationShortcut.Ctrl, roundTripped.Properties.ActivationShortcut.Ctrl);
            Assert.AreEqual(settings.Properties.ActivationShortcut.Alt, roundTripped.Properties.ActivationShortcut.Alt);
            Assert.AreEqual(settings.Properties.ActivationShortcut.Shift, roundTripped.Properties.ActivationShortcut.Shift);
            Assert.AreEqual(settings.Properties.ActivationShortcut.Code, roundTripped.Properties.ActivationShortcut.Code);
            Assert.AreEqual(settings.Properties.ActivationShortcut.Key, roundTripped.Properties.ActivationShortcut.Key);
            Assert.AreEqual(settings.Properties.AlwaysRunNotElevated.Value, roundTripped.Properties.AlwaysRunNotElevated.Value);
            Assert.AreEqual(settings.Properties.CloseAfterLosingFocus.Value, roundTripped.Properties.CloseAfterLosingFocus.Value);
            Assert.AreEqual(settings.Properties.ConfirmFileDelete.Value, roundTripped.Properties.ConfirmFileDelete.Value);
            Assert.AreEqual(settings.Properties.EnableSpaceToActivate.Value, roundTripped.Properties.EnableSpaceToActivate.Value);
            Assert.AreEqual(settings.Properties.ShowFilePreviewTooltip.Value, roundTripped.Properties.ShowFilePreviewTooltip.Value);
        }
    }
}
