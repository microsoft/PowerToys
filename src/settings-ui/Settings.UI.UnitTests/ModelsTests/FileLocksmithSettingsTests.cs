// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class FileLocksmithSettingsTests
    {
        [TestMethod]
        public void DefaultPropertiesAskForAll()
        {
            Assert.IsFalse(new FileLocksmithProperties().ConfirmOnlyForOthers.Value);
        }

        [TestMethod]
        public void LegacyLocalSettingsWithoutKeyDefaultToAskForAll()
        {
            // file-locksmith-settings.json written before the setting existed.
            var local = JsonSerializer.Deserialize("""{"showInExtendedContextMenu":true}""", SettingsSerializationContext.Default.FileLocksmithLocalProperties);

            Assert.IsNotNull(local);
            Assert.IsFalse(local.ConfirmOnlyForOthers);
            Assert.IsTrue(local.ExtendedContextMenuOnly);
        }

        [TestMethod]
        public void LocalSettingsRoundTripKeepsFlag()
        {
            var json = new FileLocksmithLocalProperties { ConfirmOnlyForOthers = true }.ToJsonString();
            StringAssert.Contains(json, "\"confirmOnlyForOthers\":true");

            var local = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.FileLocksmithLocalProperties);

            Assert.IsNotNull(local);
            Assert.IsTrue(local.ConfirmOnlyForOthers);
        }

        [TestMethod]
        public void SettingsFromLocalPropertiesMapsFlag()
        {
            var settings = new FileLocksmithSettings(new FileLocksmithLocalProperties { ConfirmOnlyForOthers = true });

            Assert.IsTrue(settings.Properties.ConfirmOnlyForOthers.Value);
        }

        [TestMethod]
        public void ModuleSettingsJsonUsesBoolPropertyShape()
        {
            // The module interface reads properties.bool_confirm_only_for_others.value (PowerToyValues::get_bool_value).
            var settings = new FileLocksmithSettings(new FileLocksmithLocalProperties { ConfirmOnlyForOthers = true });
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(settings, SettingsSerializationContext.Default.FileLocksmithSettings));

            Assert.IsTrue(document.RootElement.GetProperty("properties").GetProperty("bool_confirm_only_for_others").GetProperty("value").GetBoolean());
        }
    }
}
