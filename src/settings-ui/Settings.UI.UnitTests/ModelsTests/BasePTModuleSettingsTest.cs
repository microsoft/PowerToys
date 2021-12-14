// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UnitTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            // Mock Disk access
            var mockFileSystem = new MockFileSystem();
            var settingsUtils = new SettingsUtils(mockFileSystem);

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

            string testSettingsConfigs = new BasePTSettingsTest().ToJsonString();
            settingsUtils.SaveSettings(testSettingsConfigs, file_name);
            JsonSchema expectedSchema = JsonSchema.Parse(expectedSchemaText);

            // Act
            JObject actualSchema = JObject.Parse(settingsUtils.GetSettingsOrDefault<BasePTSettingsTest>(file_name).ToJsonString());
            bool valid = actualSchema.IsValid(expectedSchema);

            // Assert
            Assert.IsTrue(valid);
        }
    }
}
