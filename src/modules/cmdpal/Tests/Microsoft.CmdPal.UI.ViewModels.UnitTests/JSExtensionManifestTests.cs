// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class JSExtensionManifestTests
{
    private const string ValidManifestJson = """
        {
          "name": "test-extension",
          "displayName": "Test Extension",
          "version": "1.0.0",
          "description": "A test extension",
          "icon": "icon.png",
          "main": "index.js",
          "publisher": "Test Publisher",
          "engines": {
            "node": ">=18"
          },
          "capabilities": [
            "commands",
            "dynamicList"
          ]
        }
        """;

    [TestMethod]
    public void Deserialize_ValidManifest_ReturnsCorrectValues()
    {
        // Act
        var manifest = JsonSerializer.Deserialize(
            ValidManifestJson,
            JSExtensionManifestJsonContext.Default.JSExtensionManifest);

        // Assert
        Assert.IsNotNull(manifest);
        Assert.AreEqual("test-extension", manifest.Name);
        Assert.AreEqual("Test Extension", manifest.DisplayName);
        Assert.AreEqual("1.0.0", manifest.Version);
        Assert.AreEqual("A test extension", manifest.Description);
        Assert.AreEqual("icon.png", manifest.Icon);
        Assert.AreEqual("index.js", manifest.Main);
        Assert.AreEqual("Test Publisher", manifest.Publisher);
        Assert.IsNotNull(manifest.Engines);
        Assert.AreEqual(">=18", manifest.Engines.Node);
        Assert.IsNotNull(manifest.Capabilities);
        Assert.AreEqual(2, manifest.Capabilities.Length);
        Assert.AreEqual("commands", manifest.Capabilities[0]);
        Assert.AreEqual("dynamicList", manifest.Capabilities[1]);
    }

    [TestMethod]
    public void IsValid_WithRequiredFields_ReturnsTrue()
    {
        // Arrange
        var manifest = new JSExtensionManifest
        {
            Name = "test-extension",
            Main = "index.js",
        };

        // Act
        var isValid = manifest.IsValid();

        // Assert
        Assert.IsTrue(isValid);
    }

    [TestMethod]
    public void IsValid_MissingName_ReturnsFalse()
    {
        // Arrange
        var manifest = new JSExtensionManifest
        {
            Main = "index.js",
        };

        // Act
        var isValid = manifest.IsValid();

        // Assert
        Assert.IsFalse(isValid);
    }

    [TestMethod]
    public void IsValid_MissingMain_ReturnsFalse()
    {
        // Arrange
        var manifest = new JSExtensionManifest
        {
            Name = "test-extension",
        };

        // Act
        var isValid = manifest.IsValid();

        // Assert
        Assert.IsFalse(isValid);
    }

    [TestMethod]
    public void IsValid_EmptyName_ReturnsFalse()
    {
        // Arrange
        var manifest = new JSExtensionManifest
        {
            Name = string.Empty,
            Main = "index.js",
        };

        // Act
        var isValid = manifest.IsValid();

        // Assert
        Assert.IsFalse(isValid);
    }

    [TestMethod]
    public void IsValid_WhitespaceName_ReturnsFalse()
    {
        // Arrange
        var manifest = new JSExtensionManifest
        {
            Name = "   ",
            Main = "index.js",
        };

        // Act
        var isValid = manifest.IsValid();

        // Assert
        Assert.IsFalse(isValid);
    }

    [TestMethod]
    public void Deserialize_OptionalFieldsMissing_UsesDefaults()
    {
        // Arrange
        var minimalJson = """
            {
              "name": "test-extension",
              "main": "index.js"
            }
            """;

        // Act
        var manifest = JsonSerializer.Deserialize(
            minimalJson,
            JSExtensionManifestJsonContext.Default.JSExtensionManifest);

        // Assert
        Assert.IsNotNull(manifest);
        Assert.AreEqual("test-extension", manifest.Name);
        Assert.AreEqual("index.js", manifest.Main);
        Assert.IsNull(manifest.DisplayName);
        Assert.IsNull(manifest.Version);
        Assert.IsNull(manifest.Description);
        Assert.IsNull(manifest.Icon);
        Assert.IsNull(manifest.Publisher);
        Assert.IsNull(manifest.Engines);
        Assert.IsNull(manifest.Capabilities);
    }

    [TestMethod]
    public void Deserialize_CapabilitiesMissing_DefaultsToNull()
    {
        // Arrange
        var jsonWithoutCapabilities = """
            {
              "name": "test-extension",
              "main": "index.js"
            }
            """;

        // Act
        var manifest = JsonSerializer.Deserialize(
            jsonWithoutCapabilities,
            JSExtensionManifestJsonContext.Default.JSExtensionManifest);

        // Assert
        Assert.IsNotNull(manifest);
        Assert.IsNull(manifest.Capabilities);
    }

    [TestMethod]
    public void Deserialize_EmptyCapabilities_ReturnsEmptyArray()
    {
        // Arrange
        var jsonWithEmptyCapabilities = """
            {
              "name": "test-extension",
              "main": "index.js",
              "capabilities": []
            }
            """;

        // Act
        var manifest = JsonSerializer.Deserialize(
            jsonWithEmptyCapabilities,
            JSExtensionManifestJsonContext.Default.JSExtensionManifest);

        // Assert
        Assert.IsNotNull(manifest);
        Assert.IsNotNull(manifest.Capabilities);
        Assert.AreEqual(0, manifest.Capabilities.Length);
    }

    [TestMethod]
    public async Task LoadFromFileAsync_ValidFile_ReturnsManifest()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, ValidManifestJson, Encoding.UTF8);

            // Act
            var manifest = await JSExtensionManifest.LoadFromFileAsync(tempFile);

            // Assert
            Assert.IsNotNull(manifest);
            Assert.AreEqual("test-extension", manifest.Name);
            Assert.AreEqual("index.js", manifest.Main);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task LoadFromFileAsync_NonExistentFile_ReturnsNull()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");

        // Act
        var manifest = await JSExtensionManifest.LoadFromFileAsync(nonExistentPath);

        // Assert
        Assert.IsNull(manifest);
    }

    [TestMethod]
    public async Task LoadFromFileAsync_EmptyPath_ReturnsNull()
    {
        // Act
        var manifest = await JSExtensionManifest.LoadFromFileAsync(string.Empty);

        // Assert
        Assert.IsNull(manifest);
    }

    [TestMethod]
    public async Task LoadFromFileAsync_NullPath_ReturnsNull()
    {
        // Act
        var manifest = await JSExtensionManifest.LoadFromFileAsync(null!);

        // Assert
        Assert.IsNull(manifest);
    }

    [TestMethod]
    public async Task LoadFromFileAsync_MalformedJson_ReturnsNull()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var malformedJson = """
                {
                  "name": "test-extension",
                  "main": "index.js"
                  missing comma here
                }
                """;
            await File.WriteAllTextAsync(tempFile, malformedJson, Encoding.UTF8);

            // Act
            var manifest = await JSExtensionManifest.LoadFromFileAsync(tempFile);

            // Assert
            Assert.IsNull(manifest);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task LoadFromFileAsync_InvalidManifest_ReturnsNull()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var invalidJson = """
                {
                  "name": "test-extension"
                }
                """;
            await File.WriteAllTextAsync(tempFile, invalidJson, Encoding.UTF8);

            // Act
            var manifest = await JSExtensionManifest.LoadFromFileAsync(tempFile);

            // Assert
            Assert.IsNull(manifest, "Manifest missing required 'main' field should return null");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public void Deserialize_TrailingCommas_SuccessfullyDeserializes()
    {
        // Arrange
        var jsonWithTrailingCommas = """
            {
              "name": "test-extension",
              "main": "index.js",
              "version": "1.0.0",
            }
            """;

        // Act
        var manifest = JsonSerializer.Deserialize(
            jsonWithTrailingCommas,
            JSExtensionManifestJsonContext.Default.JSExtensionManifest);

        // Assert
        Assert.IsNotNull(manifest);
        Assert.AreEqual("test-extension", manifest.Name);
        Assert.AreEqual("index.js", manifest.Main);
    }

    [TestMethod]
    public void Deserialize_CaseInsensitive_SuccessfullyDeserializes()
    {
        // Arrange
        var jsonWithDifferentCase = """
            {
              "Name": "test-extension",
              "Main": "index.js",
              "DisplayName": "Test Extension"
            }
            """;

        // Act
        var manifest = JsonSerializer.Deserialize(
            jsonWithDifferentCase,
            JSExtensionManifestJsonContext.Default.JSExtensionManifest);

        // Assert
        Assert.IsNotNull(manifest);
        Assert.AreEqual("test-extension", manifest.Name);
        Assert.AreEqual("index.js", manifest.Main);
        Assert.AreEqual("Test Extension", manifest.DisplayName);
    }
}
