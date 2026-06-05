// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class CursorWrapSettingsTests
    {
        [TestMethod]
        public void JsonRoundTrip_PreservesAllFields()
        {
            var settings = new CursorWrapSettings
            {
                Name = CursorWrapSettings.ModuleName,
                Version = "1.2",
                Properties = new CursorWrapProperties
                {
                    ActivationShortcut = new HotkeySettings(true, true, false, true, 0x57)
                    {
                        Key = "W",
                    },
                    AutoActivate = new BoolProperty(true),
                    DisableWrapDuringDrag = new BoolProperty(false),
                    WrapMode = new IntProperty(2),
                    ActivationMode = new IntProperty(1),
                    DisableCursorWrapOnSingleMonitor = new BoolProperty(true),
                },
            };

            var json = settings.ToJsonString();

            var roundTripped = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.CursorWrapSettings);

            Assert.IsNotNull(roundTripped);
            Assert.AreEqual(CursorWrapSettings.ModuleName, roundTripped.Name);
            Assert.AreEqual("1.2", roundTripped.Version);
            Assert.IsNotNull(roundTripped.Properties);
            Assert.IsNotNull(roundTripped.Properties.ActivationShortcut);
            Assert.IsTrue(roundTripped.Properties.ActivationShortcut.Win);
            Assert.IsTrue(roundTripped.Properties.ActivationShortcut.Ctrl);
            Assert.IsFalse(roundTripped.Properties.ActivationShortcut.Alt);
            Assert.IsTrue(roundTripped.Properties.ActivationShortcut.Shift);
            Assert.AreEqual(0x57, roundTripped.Properties.ActivationShortcut.Code);
            Assert.AreEqual("W", roundTripped.Properties.ActivationShortcut.Key);
            Assert.IsTrue(roundTripped.Properties.AutoActivate.Value);
            Assert.IsFalse(roundTripped.Properties.DisableWrapDuringDrag.Value);
            Assert.AreEqual(2, roundTripped.Properties.WrapMode.Value);
            Assert.AreEqual(1, roundTripped.Properties.ActivationMode.Value);
            Assert.IsTrue(roundTripped.Properties.DisableCursorWrapOnSingleMonitor.Value);
        }
    }
}
