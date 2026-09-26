// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Commands;
using PowerToys.DSC.DSCResources;
using PowerToys.DSC.Models;
using PowerToys.DSC.Models.FancyZones;
using PowerToys.DSC.Models.ResourceObjects;

namespace PowerToys.DSC.UnitTests.LayoutsResourceTests;

[TestClass]
public sealed class LayoutsResourceCommandTest : BaseDscTest
{
    [TestMethod]
    public void Modules_ListsOnlyFancyZones()
    {
        // Act
        var result = ExecuteDscCommand<ModulesCommand>("--resource", LayoutsResource.ResourceName);

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(nameof(ModuleType.FancyZones), result.Output.Trim());
    }

    [TestMethod]
    public void UnsupportedModule_Fail()
    {
        // Act
        var result = ExecuteDscCommand<GetCommand>("--resource", LayoutsResource.ResourceName, "--module", "Awake");

        // Assert: the module check in BaseCommand reports a plain-text line
        // on the error stream, not a JSON message
        Assert.IsFalse(result.Success);
        Assert.AreEqual(GetResourceString("ModuleNotSupportedByResource", "Awake", LayoutsResource.ResourceName), result.Error.Trim());
    }

    [TestMethod]
    public void Schema_ContainsRequiredLayoutsPropertyWithSections()
    {
        // Act
        var result = ExecuteDscCommand<SchemaCommand>("--resource", LayoutsResource.ResourceName, "--module", nameof(ModuleType.FancyZones));

        // Assert
        Assert.IsTrue(result.Success);
        var schema = JsonNode.Parse(result.Output);
        Assert.IsNotNull(schema);
        var layouts = schema["properties"]?[LayoutsResourceObject.LayoutsJsonPropertyName];
        Assert.IsNotNull(layouts);
        var required = schema["required"]?.AsArray();
        Assert.IsNotNull(required);
        Assert.IsTrue(required.ToString().Contains(LayoutsResourceObject.LayoutsJsonPropertyName));

        var sections = ResolveReference(schema, layouts)?["properties"];
        Assert.IsNotNull(sections);
        Assert.IsNotNull(sections[FzLayoutsModel.CustomJsonPropertyName]);
        Assert.IsNotNull(sections[FzLayoutsModel.TemplatesJsonPropertyName]);
        Assert.IsNotNull(sections[FzLayoutsModel.HotkeysJsonPropertyName]);
        Assert.IsNotNull(sections[FzLayoutsModel.DefaultsJsonPropertyName]);
    }

    [TestMethod]
    public void Set_EmptyInput_Fail()
    {
        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", LayoutsResource.ResourceName, "--module", nameof(ModuleType.FancyZones));
        var messages = result.Messages();

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(DscMessageLevel.Error, messages[0].Level);
        Assert.AreEqual(GetResourceString("InputEmptyOrNullError"), messages[0].Message);
    }

    [TestMethod]
    public void Test_EmptyInput_Fail()
    {
        // Act
        var result = ExecuteDscCommand<TestCommand>("--resource", LayoutsResource.ResourceName, "--module", nameof(ModuleType.FancyZones));
        var messages = result.Messages();

        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(DscMessageLevel.Error, messages[0].Level);
        Assert.AreEqual(GetResourceString("InputEmptyOrNullError"), messages[0].Message);
    }

    [TestMethod]
    public void Manifest_WritesFancyZonesLayoutsManifest()
    {
        // Arrange
        var outputDir = Path.Combine(Path.GetTempPath(), "PowerToys.DSC.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        try
        {
            // Act
            var result = ExecuteDscCommand<ManifestCommand>("--resource", LayoutsResource.ResourceName, "--module", nameof(ModuleType.FancyZones), "--outputDir", outputDir);

            // Assert
            Assert.IsTrue(result.Success);
            var manifestPath = Path.Combine(outputDir, "microsoft.powertoys.FancyZones.layouts.dsc.resource.json");
            Assert.IsTrue(File.Exists(manifestPath), $"Manifest not found at {manifestPath}");

            var manifest = JsonNode.Parse(File.ReadAllText(manifestPath));
            Assert.IsNotNull(manifest);
            Assert.AreEqual("Microsoft.PowerToys/FancyZonesLayouts", manifest["type"]?.GetValue<string>());
            var setArgs = manifest["set"]?["args"]?.ToJsonString();
            Assert.IsNotNull(setArgs);
            StringAssert.Contains(setArgs, LayoutsResource.ResourceName);
            StringAssert.Contains(setArgs, nameof(ModuleType.FancyZones));
        }
        finally
        {
            Directory.Delete(outputDir, true);
        }
    }

    /// <summary>
    /// Resolves a "$ref" to a schema definition, if the node is a reference.
    /// NJsonSchema emits a nested object property either as a direct
    /// reference or as a "oneOf" containing the reference.
    /// </summary>
    private static JsonNode ResolveReference(JsonNode schema, JsonNode node)
    {
        var reference = node?["$ref"]?.GetValue<string>();
        if (reference == null && node?["oneOf"] is JsonArray alternatives)
        {
            reference = alternatives.Select(alternative => alternative?["$ref"]?.GetValue<string>()).FirstOrDefault(r => r != null);
        }

        if (reference == null)
        {
            return node;
        }

        var name = reference.Substring(reference.LastIndexOf('/') + 1);
        return schema["definitions"]?[name];
    }
}
