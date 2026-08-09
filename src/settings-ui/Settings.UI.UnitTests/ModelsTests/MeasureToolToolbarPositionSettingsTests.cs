// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Settings.UI.Library.Enumerations;

namespace CommonLibTest
{
    /// <summary>
    /// Enum stability / default / serialization coverage for the Screen Ruler "Toolbar position"
    /// setting. The numeric values of <see cref="MeasureToolToolbarPosition"/> are persisted in
    /// settings.json, so a regression here (e.g. someone re-ordering the enum) would silently
    /// re-map every user's saved anchor to a different one on next load.
    /// </summary>
    [TestClass]
    public class MeasureToolToolbarPositionSettingsTests
    {
        [TestMethod]
        public void EnumValues_PreservePersistedBottomAnchors()
        {
            Assert.AreEqual(0, (int)MeasureToolToolbarPosition.TopLeft);
            Assert.AreEqual(1, (int)MeasureToolToolbarPosition.TopCenter);
            Assert.AreEqual(2, (int)MeasureToolToolbarPosition.TopRight);
            Assert.AreEqual(6, (int)MeasureToolToolbarPosition.BottomLeft);
            Assert.AreEqual(7, (int)MeasureToolToolbarPosition.BottomCenter);
            Assert.AreEqual(8, (int)MeasureToolToolbarPosition.BottomRight);
            Assert.IsFalse(Enum.IsDefined(typeof(MeasureToolToolbarPosition), 3));
            Assert.IsFalse(Enum.IsDefined(typeof(MeasureToolToolbarPosition), 4));
            Assert.IsFalse(Enum.IsDefined(typeof(MeasureToolToolbarPosition), 5));
        }

        [TestMethod]
        public void Default_IsTopCenter()
        {
            var settings = new MeasureToolSettings();

            Assert.AreEqual((int)MeasureToolToolbarPosition.TopCenter, settings.Properties.ToolbarPosition.Value);
        }

        [TestMethod]
        public void ToJsonString_ShouldContainToolbarPosition()
        {
            var settings = new MeasureToolSettings();

            var json = settings.ToJsonString();

            StringAssert.Contains(json, "ToolbarPosition");
        }

        [TestMethod]
        public void RoundTrip_WithDefault_PreservesToolbarPosition()
        {
            var original = new MeasureToolSettings();

            var deserialized = JsonSerializer.Deserialize<MeasureToolSettings>(original.ToJsonString());

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.Properties.ToolbarPosition.Value, deserialized.Properties.ToolbarPosition.Value);
        }

        [DataTestMethod]
        [DataRow(MeasureToolToolbarPosition.TopLeft)]
        [DataRow(MeasureToolToolbarPosition.TopCenter)]
        [DataRow(MeasureToolToolbarPosition.TopRight)]
        [DataRow(MeasureToolToolbarPosition.BottomLeft)]
        [DataRow(MeasureToolToolbarPosition.BottomCenter)]
        [DataRow(MeasureToolToolbarPosition.BottomRight)]
        public void RoundTrip_WithEachAnchor_PreservesValue(MeasureToolToolbarPosition position)
        {
            var original = new MeasureToolSettings();
            original.Properties.ToolbarPosition.Value = (int)position;

            var deserialized = JsonSerializer.Deserialize<MeasureToolSettings>(original.ToJsonString());

            Assert.IsNotNull(deserialized);
            Assert.AreEqual((int)position, deserialized.Properties.ToolbarPosition.Value);
        }

        [TestMethod]
        public void ShouldBeRegisteredInSerializationContext()
        {
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = SettingsSerializationContext.Default,
            };

            var typeInfo = options.TypeInfoResolver?.GetTypeInfo(typeof(MeasureToolSettings), options);

            Assert.IsNotNull(typeInfo, "MeasureToolSettings must be registered in SettingsSerializationContext for Native AOT serialization.");
        }
    }
}
