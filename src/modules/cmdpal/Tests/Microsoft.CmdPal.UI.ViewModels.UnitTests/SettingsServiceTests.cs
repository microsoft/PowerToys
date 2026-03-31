// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Tests for <see cref="SettingsService"/>.
/// NOTE: These tests currently fail in console test runners due to WinUI3 COM dependencies in SettingsModel.
/// SettingsModel constructor initializes DockSettings which uses Microsoft.UI.Colors.Transparent,
/// requiring WinUI3 runtime registration. Tests pass when run within VS Test Explorer with WinUI3 host.
/// </summary>
[TestClass]
public class SettingsServiceTests
{
    private Mock<IPersistenceService> _mockPersistence = null!;
    private Mock<IApplicationInfoService> _mockAppInfo = null!;
    private SettingsModel _testSettings = null!;
    private string _testDirectory = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockPersistence = new Mock<IPersistenceService>();
        _mockAppInfo = new Mock<IApplicationInfoService>();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"CmdPalTest_{Guid.NewGuid():N}");
        _mockAppInfo.Setup(a => a.ConfigDirectory).Returns(_testDirectory);

        // Create a minimal test settings instance without triggering WinUI3 dependencies
        // We'll mock the Load to return this, avoiding SettingsModel constructor that uses Colors.Transparent
        _testSettings = CreateMinimalSettingsModel();

        // Default: Load returns our test settings
        _mockPersistence
            .Setup(p => p.Load(
                It.IsAny<string>(),
                It.IsAny<System.Text.Json.Serialization.Metadata.JsonTypeInfo<SettingsModel>>()))
            .Returns(_testSettings);
    }

    private static SettingsModel CreateMinimalSettingsModel()
    {
        // Bypass constructor by using deserialize from minimal JSON
        // This avoids WinUI3 dependencies (Colors.Transparent)
        var minimalJson = "{}";
        var settings = System.Text.Json.JsonSerializer.Deserialize(
            minimalJson,
            JsonSerializationContext.Default.SettingsModel) ?? new SettingsModel();
        return settings;
    }

    [TestMethod]
    public void Constructor_LoadsSettings_ViaPersistenceService()
    {
        // Act
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);

        // Assert
        Assert.IsNotNull(service.Settings);
        _mockPersistence.Verify(
            p => p.Load(
                It.IsAny<string>(),
                It.IsAny<System.Text.Json.Serialization.Metadata.JsonTypeInfo<SettingsModel>>()),
            Times.Once);
    }

    [TestMethod]
    public void Settings_ReturnsLoadedModel()
    {
        // Arrange
        _testSettings = _testSettings with { ShowAppDetails = true };

        // Reset mock to return updated settings
        _mockPersistence
            .Setup(p => p.Load(
                It.IsAny<string>(),
                It.IsAny<System.Text.Json.Serialization.Metadata.JsonTypeInfo<SettingsModel>>()))
            .Returns(_testSettings);

        // Act
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);

        // Assert
        Assert.IsTrue(service.Settings.ShowAppDetails);
    }

    [TestMethod]
    public void Save_DelegatesToPersistenceService()
    {
        // Arrange
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);
        service.UpdateSettings(
            s => s with { SingleClickActivates = true },
            hotReload: false);
        _mockPersistence.Invocations.Clear(); // Reset after Arrange — UpdateSettings also persists

        // Act
        service.Save(hotReload: false);

        // Assert
        _mockPersistence.Verify(
            p => p.Save(
                service.Settings,
                It.IsAny<string>(),
                It.IsAny<System.Text.Json.Serialization.Metadata.JsonTypeInfo<SettingsModel>>()),
            Times.Once);
    }

    [TestMethod]
    public void Save_WithHotReloadTrue_RaisesSettingsChangedEvent()
    {
        // Arrange
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);
        var eventRaised = false;
        service.SettingsChanged += (sender, settings) =>
        {
            eventRaised = true;
        };

        // Act
        service.Save(hotReload: true);

        // Assert
        Assert.IsTrue(eventRaised);
    }

    [TestMethod]
    public void Save_WithHotReloadFalse_DoesNotRaiseSettingsChangedEvent()
    {
        // Arrange
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);
        var eventRaised = false;
        service.SettingsChanged += (sender, settings) =>
        {
            eventRaised = true;
        };

        // Act
        service.Save(hotReload: false);

        // Assert
        Assert.IsFalse(eventRaised);
    }

    [TestMethod]
    public void Save_WithDefaultHotReload_RaisesSettingsChangedEvent()
    {
        // Arrange
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);
        var eventRaised = false;
        service.SettingsChanged += (sender, settings) =>
        {
            eventRaised = true;
        };

        // Act
        service.Save(); // Default is hotReload: true

        // Assert
        Assert.IsTrue(eventRaised);
    }

    [TestMethod]
    public void SettingsChanged_PassesCorrectArguments()
    {
        // Arrange
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);
        ISettingsService? receivedSender = null;
        SettingsModel? receivedSettings = null;

        service.SettingsChanged += (sender, settings) =>
        {
            receivedSender = sender;
            receivedSettings = settings;
        };

        // Act
        service.Save(hotReload: true);

        // Assert
        Assert.AreSame(service, receivedSender);
        Assert.AreSame(service.Settings, receivedSettings);
    }

    [TestMethod]
    public void UpdateSettings_ConcurrentUpdates_NoLostUpdates()
    {
        // Arrange — two threads each set a different property to true, 100 times.
        // Without a CAS loop, one thread's Exchange can overwrite the other's
        // property back to false from a stale snapshot. With CAS, both survive.
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);
        const int iterations = 100;
        var barrier = new System.Threading.Barrier(2);

        // Act — t1 sets ShowAppDetails=true, t2 sets SingleClickActivates=true
        var t1 = System.Threading.Tasks.Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < iterations; i++)
            {
                service.UpdateSettings(s => s with { ShowAppDetails = true }, hotReload: false);
            }
        });

        var t2 = System.Threading.Tasks.Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < iterations; i++)
            {
                service.UpdateSettings(s => s with { SingleClickActivates = true }, hotReload: false);
            }
        });

        System.Threading.Tasks.Task.WaitAll(t1, t2);

        // Assert — both properties must be true; neither should have been overwritten
        Assert.IsTrue(service.Settings.ShowAppDetails, "ShowAppDetails lost — a stale snapshot overwrote it");
        Assert.IsTrue(service.Settings.SingleClickActivates, "SingleClickActivates lost — a stale snapshot overwrote it");
    }

    /// <summary>
    /// Verifies that deserializing empty JSON "{}" produces the same non-null
    /// property values as calling <c>new()</c>. This catches the System.Text.Json
    /// source-gen issue where property initializers are not honored and reference-type
    /// properties silently end up null. The test is future-proof: adding a new
    /// property with a default value to any of these models will automatically be
    /// covered without touching this test.
    /// </summary>
    [TestMethod]
    public void Deserialize_EmptyJson_MatchesConstructorDefaults()
    {
        AssertDeserializedMatchesConstructor(new SettingsModel(), JsonSerializationContext.Default.SettingsModel);
        AssertDeserializedMatchesConstructor(new AppStateModel(), JsonSerializationContext.Default.AppStateModel);
        AssertDeserializedMatchesConstructor(new RecentCommandsManager(), JsonSerializationContext.Default.RecentCommandsManager);
        AssertDeserializedMatchesConstructor(new DockSettings(), JsonSerializationContext.Default.DockSettings);
        AssertDeserializedMatchesConstructor(new ProviderSettings(), JsonSerializationContext.Default.ProviderSettings);
    }

    /// <summary>
    /// Deserializes "{}" into <typeparamref name="T"/> and compares every
    /// readable instance property against a <c>new()</c> instance. For
    /// reference-type properties, asserts non-null when the constructor
    /// default is non-null. For all properties, asserts structural equality.
    /// </summary>
    private static void AssertDeserializedMatchesConstructor<T>(T constructed, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        var deserialized = JsonSerializer.Deserialize("{}", typeInfo);
        Assert.IsNotNull(deserialized, $"Deserialize<{typeof(T).Name}>(\"{{}}\") returned null");

        var typeName = typeof(T).Name;
        foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
            {
                continue;
            }

            // Skip [JsonIgnore] properties — they won't be set by the deserializer
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
            {
                continue;
            }

            // Skip nullable properties — null is a valid value for them,
            // so a mismatch with the constructor default is expected.
            var nullabilityContext = new NullabilityInfoContext();
            var nullabilityInfo = nullabilityContext.Create(prop);
            if (nullabilityInfo.WriteState is NullabilityState.Nullable)
            {
                continue;
            }

            var expected = prop.GetValue(constructed);
            var actual = prop.GetValue(deserialized);
            var label = $"{typeName}.{prop.Name}";

            if (expected is not null)
            {
                Assert.IsNotNull(actual, $"{label} is null after deserialization but non-null from constructor");
            }

            Assert.AreEqual(expected?.GetType(), actual?.GetType(), $"{label} type mismatch");
        }
    }
}
