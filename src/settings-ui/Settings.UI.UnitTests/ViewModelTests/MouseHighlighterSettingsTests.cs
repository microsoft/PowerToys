// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class MouseHighlighterSettingsTests
    {
        [TestMethod]
        public void JsonRoundTrip_PreservesAllFields()
        {
            var original = new MouseHighlighterProperties
            {
                LeftButtonClickColor = new StringProperty("#00FF00"),
                RightButtonClickColor = new StringProperty("#0000FF"),
                AlwaysColor = new StringProperty("#FF0000"),
                HighlightRadius = new IntProperty(35),
                HighlightFadeDelayMs = new IntProperty(750),
                HighlightFadeDurationMs = new IntProperty(400),
                AutoActivate = new BoolProperty(true),
                SpotlightMode = new BoolProperty(true),
            };

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<MouseHighlighterProperties>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.LeftButtonClickColor.Value, deserialized.LeftButtonClickColor.Value);
            Assert.AreEqual(original.RightButtonClickColor.Value, deserialized.RightButtonClickColor.Value);
            Assert.AreEqual(original.AlwaysColor.Value, deserialized.AlwaysColor.Value);
            Assert.AreEqual(original.HighlightOpacity.Value, deserialized.HighlightOpacity.Value);
            Assert.AreEqual(original.HighlightRadius.Value, deserialized.HighlightRadius.Value);
            Assert.AreEqual(original.HighlightFadeDelayMs.Value, deserialized.HighlightFadeDelayMs.Value);
            Assert.AreEqual(original.HighlightFadeDurationMs.Value, deserialized.HighlightFadeDurationMs.Value);
            Assert.AreEqual(original.AutoActivate.Value, deserialized.AutoActivate.Value);
            Assert.AreEqual(original.SpotlightMode.Value, deserialized.SpotlightMode.Value);
            Assert.AreEqual(original.ActivationShortcut.Code, deserialized.ActivationShortcut.Code);
        }
    }
}
