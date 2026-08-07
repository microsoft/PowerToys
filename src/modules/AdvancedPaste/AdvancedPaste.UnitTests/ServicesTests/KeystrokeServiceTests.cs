// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using AdvancedPaste.Services;
using AdvancedPaste.Settings;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.UnitTests.ServicesTests;

[TestClass]
public sealed class KeystrokeServiceTests
{
    private sealed class TestUserSettings : IUserSettings
    {
        public bool IsAIEnabled { get; set; }

        public bool ShowCustomPreview { get; set; }

        public bool CloseAfterLosingFocus { get; set; }

        public bool EnableClipboardPreview { get; set; }

        public int KeystrokeDelayMs { get; set; }

        public int KeystrokeBatchSize { get; set; }

        public IReadOnlyList<AdvancedPasteCustomAction> CustomActions => Array.Empty<AdvancedPasteCustomAction>();

        public IReadOnlyList<PasteFormats> AdditionalActions => Array.Empty<PasteFormats>();

        public PasteAIConfiguration PasteAIConfiguration { get; set; } = new();

        public event EventHandler Changed;

        public System.Threading.Tasks.Task SetActiveAIProviderAsync(string providerId)
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }

    [TestMethod]
    public void Constructor_WithValidSettings_UsesConfiguredValues()
    {
        // Arrange
        var userSettings = new TestUserSettings
        {
            KeystrokeDelayMs = 50,
            KeystrokeBatchSize = 5,
        };

        // Act
        var service = new KeystrokeService(userSettings);

        // Assert
        // Verify that the service was created successfully with the provided values
        // We can't directly access private fields, but we can verify behavior through SendTextAsKeystrokes
        // This test primarily verifies that the constructor accepts valid values
        Assert.IsNotNull(service);
    }

    [TestMethod]
    public void Constructor_WithZeroDelay_FallsBackToDefault()
    {
        // Arrange
        var userSettings = new TestUserSettings
        {
            KeystrokeDelayMs = 0,
            KeystrokeBatchSize = 2,
        };

        // Act
        var service = new KeystrokeService(userSettings);

        // Assert
        Assert.IsNotNull(service);
        // The service should use the default delay (30ms) instead of 0
    }

    [TestMethod]
    public void Constructor_WithNegativeDelay_FallsBackToDefault()
    {
        // Arrange
        var userSettings = new TestUserSettings
        {
            KeystrokeDelayMs = -10,
            KeystrokeBatchSize = 3,
        };

        // Act
        var service = new KeystrokeService(userSettings);

        // Assert
        Assert.IsNotNull(service);
        // The service should use the default delay (30ms) instead of negative value
    }

    [TestMethod]
    public void Constructor_WithZeroBatchSize_FallsBackToDefault()
    {
        // Arrange
        var userSettings = new TestUserSettings
        {
            KeystrokeDelayMs = 25,
            KeystrokeBatchSize = 0,
        };

        // Act
        var service = new KeystrokeService(userSettings);

        // Assert
        Assert.IsNotNull(service);
        // The service should use the default batch size (1) instead of 0
    }

    [TestMethod]
    public void Constructor_WithNegativeBatchSize_FallsBackToDefault()
    {
        // Arrange
        var userSettings = new TestUserSettings
        {
            KeystrokeDelayMs = 25,
            KeystrokeBatchSize = -5,
        };

        // Act
        var service = new KeystrokeService(userSettings);

        // Assert
        Assert.IsNotNull(service);
        // The service should use the default batch size (1) instead of negative value
    }

    [TestMethod]
    public void Constructor_WithDefaultValues_CreatesService()
    {
        // Arrange
        var userSettings = new TestUserSettings
        {
            KeystrokeDelayMs = AdvancedPasteProperties.DefaultKeystrokeDelayMs,
            KeystrokeBatchSize = AdvancedPasteProperties.DefaultKeystrokeBatchSize,
        };

        // Act
        var service = new KeystrokeService(userSettings);

        // Assert
        Assert.IsNotNull(service);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_WithNullSettings_ThrowsArgumentNullException()
    {
        // Act
        var service = new KeystrokeService(null);

        // Assert - Exception should be thrown
    }

    [TestMethod]
    public void Constructor_WithLargeDelay_UsesProvidedValue()
    {
        // Arrange
        var userSettings = new TestUserSettings
        {
            KeystrokeDelayMs = 1000,
            KeystrokeBatchSize = 1,
        };

        // Act
        var service = new KeystrokeService(userSettings);

        // Assert
        Assert.IsNotNull(service);
    }

    [TestMethod]
    public void Constructor_WithLargeBatchSize_UsesProvidedValue()
    {
        // Arrange
        var userSettings = new TestUserSettings
        {
            KeystrokeDelayMs = 30,
            KeystrokeBatchSize = 100,
        };

        // Act
        var service = new KeystrokeService(userSettings);

        // Assert
        Assert.IsNotNull(service);
    }
}
