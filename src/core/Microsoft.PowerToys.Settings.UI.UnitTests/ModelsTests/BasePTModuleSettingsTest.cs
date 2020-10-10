// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UnitTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace CommonLibTest
{
    [TestClass]
    public class BasePTModuleSettingsTest
    {
        // Work around for System.JSON required properties:
        // https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-migrate-from-newtonsoft-how-to.
        // Test also fails when the attributes are not initialized i.e they have null values.
        [TestMethod]
        [ObsoleteAttribute("This test method is obsolete.", true)]
        public void ToJsonStringShouldReturnValidJSONOfModelWhenSuccessful()
        {
            //Mock Disk access
            string saveContent = string.Empty;
            string savePath = string.Empty;
            var mockIOProvider = IIOProviderMocks.GetMockIOProviderForSaveLoadExists();

            var settingsUtils = new SettingsUtils(mockIOProvider.Object);

            // Arrange
            string file_name = "test\\BasePTModuleSettingsTest";
            string expectedSchemaText = @"
                {
                    '$schema': 'http://json-schema.org/draft-04/schema#',
                    'type': 'object',
                    'properties': {
                    'name': {
                        'type': 'string'
                    },
                    'version': {
                        'type': 'string'
                    }
                    },
                'additionalProperties': false
                }";

            var testSettingsConfigs = new BasePTSettingsTest();
            settingsUtils.SaveSettings(testSettingsConfigs, file_name);
            JsonSchema expectedSchema = JsonSchema.Parse(expectedSchemaText);

            // Act
            JObject actualSchema = JObject.Parse(JsonSerializer.Serialize(settingsUtils.GetSettings<BasePTSettingsTest>(file_name), typeof(BasePTSettingsTest)));
            bool valid = actualSchema.IsValid(expectedSchema);

            // Assert
            Assert.IsTrue(valid);
        }
    }
}
