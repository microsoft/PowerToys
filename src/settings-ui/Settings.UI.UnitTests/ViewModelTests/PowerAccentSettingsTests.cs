// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class PowerAccentSettingsTests
    {
        [TestMethod]
        public void Deserialization_FromPartialJson_FillsDefaults()
        {
            const string json = "{\"activation_key\":1,\"show_description\":true}";

            var deserialized = JsonSerializer.Deserialize<PowerAccentProperties>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(PowerAccentActivationKey.Space, deserialized.ActivationKey);
            Assert.IsTrue(deserialized.ShowUnicodeDescription);
            Assert.IsTrue(deserialized.DoNotActivateOnGameMode);
            Assert.AreEqual("Top center", deserialized.ToolbarPosition.Value);
            Assert.AreEqual(PowerAccentSettings.DefaultInputTimeMs, deserialized.InputTime.Value);
            Assert.AreEqual("ALL", deserialized.SelectedLang.Value);
            Assert.AreEqual(string.Empty, deserialized.ExcludedApps.Value);
        }
    }
}
