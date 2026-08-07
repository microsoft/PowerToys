// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.UnitTests.HelpersTests;

[TestClass]
public sealed class AdvancedPastePropertiesTests
{
    [TestMethod]
    public void Constructor_InitializesWithDefaults()
    {
        // Act
        var properties = new AdvancedPasteProperties();

        // Assert
        Assert.AreEqual(AdvancedPasteProperties.DefaultKeystrokeDelayMs, properties.KeystrokeDelayMs);
        Assert.AreEqual(AdvancedPasteProperties.DefaultKeystrokeBatchSize, properties.KeystrokeBatchSize);
    }

    [TestMethod]
    public void Constructor_DefaultDelayIsThirty()
    {
        // Assert
        Assert.AreEqual(30, AdvancedPasteProperties.DefaultKeystrokeDelayMs);
    }

    [TestMethod]
    public void Constructor_DefaultBatchSizeIsOne()
    {
        // Assert
        Assert.AreEqual(1, AdvancedPasteProperties.DefaultKeystrokeBatchSize);
    }

    [TestMethod]
    public void JsonSerialization_PreservesKeystrokeDelay()
    {
        // Arrange
        var properties = new AdvancedPasteProperties
        {
            KeystrokeDelayMs = 50,
        };

        // Act
        var json = properties.ToString();
        var deserialized = JsonSerializer.Deserialize<AdvancedPasteProperties>(json);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(50, deserialized.KeystrokeDelayMs);
    }

    [TestMethod]
    public void JsonSerialization_PreservesKeystrokeBatchSize()
    {
        // Arrange
        var properties = new AdvancedPasteProperties
        {
            KeystrokeBatchSize = 10,
        };

        // Act
        var json = properties.ToString();
        var deserialized = JsonSerializer.Deserialize<AdvancedPasteProperties>(json);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(10, deserialized.KeystrokeBatchSize);
    }

    [TestMethod]
    public void JsonSerialization_PreservesBothKeystrokeSettings()
    {
        // Arrange
        var properties = new AdvancedPasteProperties
        {
            KeystrokeDelayMs = 100,
            KeystrokeBatchSize = 5,
        };

        // Act
        var json = properties.ToString();
        var deserialized = JsonSerializer.Deserialize<AdvancedPasteProperties>(json);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(100, deserialized.KeystrokeDelayMs);
        Assert.AreEqual(5, deserialized.KeystrokeBatchSize);
    }

    [TestMethod]
    public void JsonSerialization_WithZeroValues()
    {
        // Arrange
        var properties = new AdvancedPasteProperties
        {
            KeystrokeDelayMs = 0,
            KeystrokeBatchSize = 0,
        };

        // Act
        var json = properties.ToString();
        var deserialized = JsonSerializer.Deserialize<AdvancedPasteProperties>(json);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(0, deserialized.KeystrokeDelayMs);
        Assert.AreEqual(0, deserialized.KeystrokeBatchSize);
    }

    [TestMethod]
    public void JsonSerialization_WithNegativeValues()
    {
        // Arrange
        var properties = new AdvancedPasteProperties
        {
            KeystrokeDelayMs = -10,
            KeystrokeBatchSize = -5,
        };

        // Act
        var json = properties.ToString();
        var deserialized = JsonSerializer.Deserialize<AdvancedPasteProperties>(json);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(-10, deserialized.KeystrokeDelayMs);
        Assert.AreEqual(-5, deserialized.KeystrokeBatchSize);
    }

    [TestMethod]
    public void JsonPropertyNames_AreCorrect()
    {
        // Arrange
        var properties = new AdvancedPasteProperties
        {
            KeystrokeDelayMs = 75,
            KeystrokeBatchSize = 8,
        };

        // Act
        var json = properties.ToString();

        // Assert
        Assert.IsTrue(json.Contains("keystroke-delay-ms"), "JSON should contain 'keystroke-delay-ms' property");
        Assert.IsTrue(json.Contains("keystroke-batch-size"), "JSON should contain 'keystroke-batch-size' property");
    }

    [TestMethod]
    public void JsonDeserialization_HandlesDefaultValues()
    {
        // Arrange
        var json = @"{
            ""keystroke-delay-ms"": 30,
            ""keystroke-batch-size"": 1
        }";

        // Act
        var deserialized = JsonSerializer.Deserialize<AdvancedPasteProperties>(json);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(30, deserialized.KeystrokeDelayMs);
        Assert.AreEqual(1, deserialized.KeystrokeBatchSize);
    }

    [TestMethod]
    public void JsonDeserialization_HandlesMissingProperties_UsesDefaults()
    {
        // Arrange
        var json = @"{}";

        // Act
        var deserialized = JsonSerializer.Deserialize<AdvancedPasteProperties>(json);

        // Assert
        Assert.IsNotNull(deserialized);
        // Missing properties should have default values of 0 (not initialized)
        Assert.AreEqual(0, deserialized.KeystrokeDelayMs);
        Assert.AreEqual(0, deserialized.KeystrokeBatchSize);
    }
}
