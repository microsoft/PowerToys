// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class PersistenceServiceTests
{
    private PersistenceService _service = null!;
    private string _testDirectory = null!;
    private string _testFilePath = null!;

    // Simple test model for persistence testing
    private sealed class TestModel
    {
        public string Name { get; set; } = string.Empty;

        public int Value { get; set; }
    }

    [JsonSerializable(typeof(TestModel))]
    private sealed partial class TestJsonContext : JsonSerializerContext
    {
    }

    [TestInitialize]
    public void Setup()
    {
        _service = new PersistenceService();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"PersistenceServiceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _testFilePath = Path.Combine(_testDirectory, "test.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Load_ReturnsNewInstance_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.json");

        // Act
        var result = _service.Load(nonExistentPath, TestJsonContext.Default.TestModel);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(string.Empty, result.Name);
        Assert.AreEqual(0, result.Value);
    }

    [TestMethod]
    public void Load_ReturnsDeserializedModel_WhenFileContainsValidJson()
    {
        // Arrange
        var expectedModel = new TestModel { Name = "Test", Value = 42 };
        var json = JsonSerializer.Serialize(expectedModel, TestJsonContext.Default.TestModel);
        File.WriteAllText(_testFilePath, json);

        // Act
        var result = _service.Load(_testFilePath, TestJsonContext.Default.TestModel);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Test", result.Name);
        Assert.AreEqual(42, result.Value);
    }

    [TestMethod]
    public void Load_ReturnsNewInstance_WhenFileContainsInvalidJson()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{ invalid json }");

        // Act
        var result = _service.Load(_testFilePath, TestJsonContext.Default.TestModel);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(string.Empty, result.Name);
        Assert.AreEqual(0, result.Value);
    }

    [TestMethod]
    public void Save_CreatesFile_WhenFileDoesNotExist()
    {
        // Arrange
        var model = new TestModel { Name = "NewFile", Value = 123 };

        // Act
        _service.Save(model, _testFilePath, TestJsonContext.Default.TestModel);

        // Assert
        Assert.IsTrue(File.Exists(_testFilePath));
        var content = File.ReadAllText(_testFilePath);
        Assert.IsTrue(content.Contains("NewFile"));
        Assert.IsTrue(content.Contains("123"));
    }

    [TestMethod]
    public void Save_ShallowMerges_PreservingUnknownKeys()
    {
        // Arrange
        // Write initial file with an extra unknown key
        var initialJson = """
        {
            "Name": "OldName",
            "Value": 999,
            "UnknownKey": "ShouldBePreserved"
        }
        """;
        File.WriteAllText(_testFilePath, initialJson);

        var updatedModel = new TestModel { Name = "NewName", Value = 777 };

        // Act
        _service.Save(updatedModel, _testFilePath, TestJsonContext.Default.TestModel);

        // Assert
        var savedContent = File.ReadAllText(_testFilePath);
        var savedObject = JsonNode.Parse(savedContent) as JsonObject;
        Assert.IsNotNull(savedObject);
        Assert.AreEqual("NewName", savedObject["Name"]?.GetValue<string>());
        Assert.AreEqual(777, savedObject["Value"]?.GetValue<int>());
        Assert.AreEqual("ShouldBePreserved", savedObject["UnknownKey"]?.GetValue<string>());
    }

    [TestMethod]
    public void Save_OverwritesExistingKeys_WithNewValues()
    {
        // Arrange
        var initialModel = new TestModel { Name = "Initial", Value = 100 };
        _service.Save(initialModel, _testFilePath, TestJsonContext.Default.TestModel);

        var updatedModel = new TestModel { Name = "Updated", Value = 200 };

        // Act
        _service.Save(updatedModel, _testFilePath, TestJsonContext.Default.TestModel);

        // Assert
        var result = _service.Load(_testFilePath, TestJsonContext.Default.TestModel);
        Assert.AreEqual("Updated", result.Name);
        Assert.AreEqual(200, result.Value);
    }

    [TestMethod]
    public void Save_InvokesPostProcessMerge_WhenProvided()
    {
        // Arrange
        var model = new TestModel { Name = "Test", Value = 42 };
        var postProcessCalled = false;
        JsonObject? capturedMerged = null;

        void PostProcess(JsonObject merged)
        {
            postProcessCalled = true;
            capturedMerged = merged;
            merged.Remove("Name"); // Remove a key in postProcess
        }

        // Act
        _service.Save(model, _testFilePath, TestJsonContext.Default.TestModel, PostProcess);

        // Assert
        Assert.IsTrue(postProcessCalled);
        Assert.IsNotNull(capturedMerged);

        var savedContent = File.ReadAllText(_testFilePath);
        var savedObject = JsonNode.Parse(savedContent) as JsonObject;
        Assert.IsNotNull(savedObject);
        Assert.IsFalse(savedObject.ContainsKey("Name")); // Should be removed by postProcess
        Assert.AreEqual(42, savedObject["Value"]?.GetValue<int>());
    }

    [TestMethod]
    public void Save_HandlesExistingDirectory()
    {
        // Arrange
        // Note: PersistenceService.Save() does NOT create missing directories
        // It relies on Directory.CreateDirectory being called by the consumer
        // (e.g., SettingsService calls Directory.CreateDirectory in SettingsJsonPath())
        var nestedDir = Path.Combine(_testDirectory, "nested");
        Directory.CreateDirectory(nestedDir); // Create directory beforehand
        var nestedFilePath = Path.Combine(nestedDir, "test.json");
        var model = new TestModel { Name = "NestedTest", Value = 321 };

        // Act
        _service.Save(model, nestedFilePath, TestJsonContext.Default.TestModel);

        // Assert
        Assert.IsTrue(File.Exists(nestedFilePath));
        var result = _service.Load(nestedFilePath, TestJsonContext.Default.TestModel);
        Assert.AreEqual("NestedTest", result.Name);
        Assert.AreEqual(321, result.Value);
    }
}
