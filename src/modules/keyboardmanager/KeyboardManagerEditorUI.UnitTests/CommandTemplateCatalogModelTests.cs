// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Text.Json;

using KeyboardManagerEditorUI.Templates;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    /// <summary>
    /// Validates that the catalog data model deserializes the shipped powertoyscli.json schema
    /// shape correctly. (Reflection-based deserialization mirrors the source-generated context.)
    /// </summary>
    [TestClass]
    public class CommandTemplateCatalogModelTests
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private const string SeedCatalog = @"
        {
          ""schemaVersion"": 1,
          ""modules"": [
            {
              ""id"": ""settings"",
              ""displayResourceKey"": ""TemplateModule_Settings"",
              ""iconGlyph"": """",
              ""commands"": [
                {
                  ""id"": ""settings.openMain"",
                  ""displayResourceKey"": ""TemplateCmd_Settings_OpenMain"",
                  ""executable"": ""%LOCALAPPDATA%\\PowerToys\\PowerToys.exe"",
                  ""argsTemplate"": ""--open-settings"",
                  ""parameters"": []
                },
                {
                  ""id"": ""settings.openModule"",
                  ""displayResourceKey"": ""TemplateCmd_Settings_OpenModule"",
                  ""executable"": ""%LOCALAPPDATA%\\PowerToys\\PowerToys.exe"",
                  ""argsTemplate"": ""--open-settings={module}"",
                  ""parameters"": [
                    {
                      ""name"": ""module"",
                      ""labelResourceKey"": ""TemplateParam_Module"",
                      ""type"": ""Combo"",
                      ""required"": true,
                      ""choices"": [
                        { ""value"": ""ColorPicker"", ""displayResourceKey"": ""Module_ColorPicker"" }
                      ]
                    }
                  ]
                }
              ]
            }
          ]
        }";

        [TestMethod]
        public void Deserialize_SeedCatalog_MapsAllFields()
        {
            var catalog = JsonSerializer.Deserialize<PowerToysCliCatalog>(SeedCatalog, Options);

            Assert.IsNotNull(catalog);
            Assert.AreEqual(1, catalog!.SchemaVersion);
            Assert.AreEqual(1, catalog.Modules.Count);

            var module = catalog.Modules[0];
            Assert.AreEqual("settings", module.Id);
            Assert.AreEqual("TemplateModule_Settings", module.DisplayResourceKey);
            Assert.AreEqual(2, module.Commands.Count);

            var openMain = module.Commands.Single(c => c.Id == "settings.openMain");
            Assert.AreEqual("--open-settings", openMain.ArgsTemplate);
            Assert.AreEqual(0, openMain.Parameters.Count);

            var openModule = module.Commands.Single(c => c.Id == "settings.openModule");
            Assert.AreEqual("--open-settings={module}", openModule.ArgsTemplate);
            Assert.AreEqual(1, openModule.Parameters.Count);

            var param = openModule.Parameters[0];
            Assert.AreEqual("module", param.Name);
            Assert.AreEqual("Combo", param.Type);
            Assert.IsTrue(param.Required);
            Assert.IsNotNull(param.Choices);
            Assert.AreEqual("ColorPicker", param.Choices![0].Value);
            Assert.AreEqual("Module_ColorPicker", param.Choices[0].DisplayResourceKey);
        }

        [TestMethod]
        public void Deserialize_ForwardCompatibleVersion_StillParses()
        {
            // A newer, additive schemaVersion with an unknown field must deserialize (unknown fields ignored).
            const string newer = @"
            {
              ""schemaVersion"": 2,
              ""futureField"": ""ignored"",
              ""modules"": [
                { ""id"": ""m"", ""displayResourceKey"": ""k"", ""commands"": [] }
              ]
            }";

            var catalog = JsonSerializer.Deserialize<PowerToysCliCatalog>(newer, Options);

            Assert.IsNotNull(catalog);
            Assert.AreEqual(2, catalog!.SchemaVersion);
            Assert.AreEqual(1, catalog.Modules.Count);
        }

        [TestMethod]
        public void Deserialize_OptionalChoices_DefaultToNull()
        {
            var param = JsonSerializer.Deserialize<TemplateParameter>(
                @"{ ""name"": ""p"", ""type"": ""Text"" }", Options);

            Assert.IsNotNull(param);
            Assert.AreEqual("p", param!.Name);
            Assert.IsNull(param.Choices);
        }
    }
}
