// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Settings
{
    [TestClass]
    public class UserSettingsTests
    {
        [TestMethod]
        public void Default_settings_version_is_2_1()
        {
            Assert.AreEqual("2.1", new ColorPickerSettings().Version);
        }

        [TestMethod]
        public void Properties_serialize_with_lowercase_keys()
        {
            var json = JsonSerializer.Serialize(new ColorPickerProperties());
            StringAssert.Contains(json, "\"showcolorname\"");
            StringAssert.Contains(json, "\"copiedcolorrepresentation\"");
            StringAssert.Contains(json, "\"changecursor\"");
            StringAssert.Contains(json, "\"samplesize\"");
        }

        [TestMethod]
        public void Sample_size_defaults_to_a_single_pixel()
        {
            Assert.AreEqual(1, new ColorPickerProperties().SampleSize);
        }

        [TestMethod]
        public void Sample_size_defaults_to_a_single_pixel_when_missing_from_json()
        {
            // Settings files written before the sample size option existed don't have the key.
            var properties = JsonSerializer.Deserialize<ColorPickerProperties>("{\"showcolorname\":{\"value\":true}}");
            Assert.IsNotNull(properties);
            Assert.AreEqual(1, properties.SampleSize);
        }

        [TestMethod]
        [DataRow(1, 1)]
        [DataRow(3, 3)]
        [DataRow(9, 9)]
        [DataRow(25, 25)]
        [DataRow(4, 3)]
        [DataRow(10, 9)]
        [DataRow(24, 9)]
        [DataRow(0, 1)]
        [DataRow(-5, 1)]
        [DataRow(100, 25)]
        public void NormalizeSampleSize_returns_largest_supported_size_not_above_value(int sampleSize, int expected)
        {
            Assert.AreEqual(expected, ColorPickerProperties.NormalizeSampleSize(sampleSize));
        }
    }
}
