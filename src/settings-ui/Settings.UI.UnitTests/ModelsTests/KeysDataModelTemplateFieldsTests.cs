// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UnitTest.ModelsTests
{
    [TestClass]
    public class KeysDataModelTemplateFieldsTests
    {
        [TestMethod]
        public void TemplateFields_RoundTripThroughJson()
        {
            var original = new KeysDataModel
            {
                OriginalKeys = "162;67",
                NewRemapKeys = string.Empty,
                OperationType = 1,
                RunProgramFilePath = "%LOCALAPPDATA%\\PowerToys\\PowerToys.exe",
                RunProgramArgs = "--open-settings=ColorPicker",
                TemplateId = "settings.openModule",
                TemplateParameters = new Dictionary<string, string>
                {
                    { "module", "ColorPicker" },
                },
            };

            var json = JsonSerializer.Serialize(original);
            var decoded = JsonSerializer.Deserialize<KeysDataModel>(json);

            Assert.AreEqual("settings.openModule", decoded.TemplateId);
            Assert.IsNotNull(decoded.TemplateParameters);
            Assert.AreEqual("ColorPicker", decoded.TemplateParameters["module"]);
        }

        [TestMethod]
        public void TemplateFields_OmittedFromJsonWhenNull()
        {
            var entry = new KeysDataModel
            {
                OriginalKeys = "162;67",
                NewRemapKeys = "162;86",
                OperationType = 0,
                TemplateId = null,
                TemplateParameters = null,
            };

            var json = JsonSerializer.Serialize(entry);

            Assert.IsFalse(json.Contains("templateId"), "templateId should be omitted when null");
            Assert.IsFalse(json.Contains("templateParameters"), "templateParameters should be omitted when null");
        }

        [TestMethod]
        public void TemplateFields_PresentInJsonWhenSet()
        {
            var entry = new KeysDataModel
            {
                OriginalKeys = "162;67",
                NewRemapKeys = string.Empty,
                OperationType = 1,
                RunProgramFilePath = "%LOCALAPPDATA%\\PowerToys\\PowerToys.exe",
                RunProgramArgs = "--open-settings",
                TemplateId = "settings.openMain",
                TemplateParameters = new Dictionary<string, string>(),
            };

            var json = JsonSerializer.Serialize(entry);

            Assert.IsTrue(json.Contains("\"templateId\""), "templateId is non-null, should be serialized");
        }
    }
}
