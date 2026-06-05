// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class MousePointerCrosshairsSettingsTests
    {
        [TestMethod]
        public void JsonRoundTrip_PreservesAllFields()
        {
            var original = new MousePointerCrosshairsProperties
            {
                CrosshairsColor = new StringProperty("#00FF00"),
                CrosshairsOpacity = new IntProperty(50),
                CrosshairsRadius = new IntProperty(30),
                CrosshairsThickness = new IntProperty(7),
                CrosshairsBorderColor = new StringProperty("#000000"),
                CrosshairsBorderSize = new IntProperty(2),
                CrosshairsOrientation = new IntProperty(1),
                CrosshairsAutoHide = new BoolProperty(true),
                CrosshairsIsFixedLengthEnabled = new BoolProperty(true),
                CrosshairsFixedLength = new IntProperty(500),
                AutoActivate = new BoolProperty(true),
                GlidingTravelSpeed = new IntProperty(40),
                GlidingDelaySpeed = new IntProperty(10),
            };

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<MousePointerCrosshairsProperties>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.CrosshairsColor.Value, deserialized.CrosshairsColor.Value);
            Assert.AreEqual(original.CrosshairsOpacity.Value, deserialized.CrosshairsOpacity.Value);
            Assert.AreEqual(original.CrosshairsRadius.Value, deserialized.CrosshairsRadius.Value);
            Assert.AreEqual(original.CrosshairsThickness.Value, deserialized.CrosshairsThickness.Value);
            Assert.AreEqual(original.CrosshairsBorderColor.Value, deserialized.CrosshairsBorderColor.Value);
            Assert.AreEqual(original.CrosshairsBorderSize.Value, deserialized.CrosshairsBorderSize.Value);
            Assert.AreEqual(original.CrosshairsOrientation.Value, deserialized.CrosshairsOrientation.Value);
            Assert.AreEqual(original.CrosshairsAutoHide.Value, deserialized.CrosshairsAutoHide.Value);
            Assert.AreEqual(original.CrosshairsIsFixedLengthEnabled.Value, deserialized.CrosshairsIsFixedLengthEnabled.Value);
            Assert.AreEqual(original.CrosshairsFixedLength.Value, deserialized.CrosshairsFixedLength.Value);
            Assert.AreEqual(original.AutoActivate.Value, deserialized.AutoActivate.Value);
            Assert.AreEqual(original.GlidingTravelSpeed.Value, deserialized.GlidingTravelSpeed.Value);
            Assert.AreEqual(original.GlidingDelaySpeed.Value, deserialized.GlidingDelaySpeed.Value);
            Assert.AreEqual(original.ActivationShortcut.Code, deserialized.ActivationShortcut.Code);
            Assert.AreEqual(original.GlidingCursorActivationShortcut.Code, deserialized.GlidingCursorActivationShortcut.Code);
        }
    }
}
