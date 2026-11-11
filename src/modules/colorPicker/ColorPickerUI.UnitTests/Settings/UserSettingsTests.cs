// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Settings
{
    [OSCondition(OperatingSystems.Windows)]
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
            Assert.Contains("\"showcolorname\"", json);
            Assert.Contains("\"copiedcolorrepresentation\"", json);
            Assert.Contains("\"changecursor\"", json);
        }
    }
}
