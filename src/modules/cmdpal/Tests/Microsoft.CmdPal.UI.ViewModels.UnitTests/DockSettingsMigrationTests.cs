// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Tests for dock settings migration from single-monitor (ShowLabels) to
/// multi-monitor (ShowTitles/ShowSubtitles) format.
/// </summary>
[TestClass]
public class DockSettingsMigrationTests
{
    private string _testDirectory = null!;
    private string _settingsFilePath = null!;
    private Mock<IPersistenceService> _mockPersistence = null!;
    private Mock<IApplicationInfoService> _mockAppInfo = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"CmdPalMigrationTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _settingsFilePath = Path.Combine(_testDirectory, "settings.json");

        _mockPersistence = new Mock<IPersistenceService>();
        _mockAppInfo = new Mock<IApplicationInfoService>();
        _mockAppInfo.Setup(a => a.ConfigDirectory).Returns(_testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    [TestMethod]
    public void Migration_ShowLabels_MigratedToShowTitlesAndShowSubtitles()
    {
        // Arrange: old-format JSON with ShowLabels on bands
        var oldJson = """
        {
            "DockSettings": {
                "StartBands": [
                    { "ProviderId": "p1", "CommandId": "c1", "ShowLabels": false },
                    { "ProviderId": "p2", "CommandId": "c2", "ShowLabels": true }
                ],
                "CenterBands": [],
                "EndBands": [
                    { "ProviderId": "p3", "CommandId": "c3", "ShowLabels": false }
                ]
            }
        }
        """;
        File.WriteAllText(_settingsFilePath, oldJson);

        // Model as deserialized (ShowLabels is [JsonIgnore], so ShowTitles/ShowSubtitles are null)
        var model = CreateModelWithBands(
            startBands: ImmutableList.Create(
                new DockBandSettings { ProviderId = "p1", CommandId = "c1" },
                new DockBandSettings { ProviderId = "p2", CommandId = "c2" }),
            endBands: ImmutableList.Create(
                new DockBandSettings { ProviderId = "p3", CommandId = "c3" }));

        SetupMockLoad(model);
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);

        // Assert: ShowTitles and ShowSubtitles should have the old ShowLabels values
        var start = service.Settings.DockSettings.StartBands;
        Assert.AreEqual(false, start[0].ShowTitles, "Band 0 ShowTitles should be false");
        Assert.AreEqual(false, start[0].ShowSubtitles, "Band 0 ShowSubtitles should be false");
        Assert.AreEqual(true, start[1].ShowTitles, "Band 1 ShowTitles should be true");
        Assert.AreEqual(true, start[1].ShowSubtitles, "Band 1 ShowSubtitles should be true");

        var end = service.Settings.DockSettings.EndBands;
        Assert.AreEqual(false, end[0].ShowTitles, "End band ShowTitles should be false");
        Assert.AreEqual(false, end[0].ShowSubtitles, "End band ShowSubtitles should be false");

        // Save should have been called (migration triggers Save)
        _mockPersistence.Verify(
            p => p.Save(
                It.IsAny<SettingsModel>(),
                It.IsAny<string>(),
                It.IsAny<System.Text.Json.Serialization.Metadata.JsonTypeInfo<SettingsModel>>()),
            Times.AtLeastOnce);
    }

    [TestMethod]
    public void Migration_AlreadyMigrated_ShowTitlesPresent_NoOp()
    {
        // Arrange: new-format JSON already has ShowTitles
        var newJson = """
        {
            "DockSettings": {
                "StartBands": [
                    { "ProviderId": "p1", "CommandId": "c1", "ShowTitles": false, "ShowSubtitles": true }
                ],
                "CenterBands": [],
                "EndBands": []
            }
        }
        """;
        File.WriteAllText(_settingsFilePath, newJson);

        var model = CreateModelWithBands(
            startBands: ImmutableList.Create(
                new DockBandSettings { ProviderId = "p1", CommandId = "c1", ShowTitles = false, ShowSubtitles = true }));

        SetupMockLoad(model);
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);

        // Assert: values unchanged
        Assert.AreEqual(false, service.Settings.DockSettings.StartBands[0].ShowTitles);
        Assert.AreEqual(true, service.Settings.DockSettings.StartBands[0].ShowSubtitles);
    }

    [TestMethod]
    public void Migration_MixedBands_OnlyUnmigratedBandsTouched()
    {
        // Arrange: one band has old ShowLabels, another already has ShowTitles
        var mixedJson = """
        {
            "DockSettings": {
                "StartBands": [
                    { "ProviderId": "p1", "CommandId": "c1", "ShowLabels": false },
                    { "ProviderId": "p2", "CommandId": "c2", "ShowTitles": true, "ShowSubtitles": false }
                ],
                "CenterBands": [],
                "EndBands": []
            }
        }
        """;
        File.WriteAllText(_settingsFilePath, mixedJson);

        var model = CreateModelWithBands(
            startBands: ImmutableList.Create(
                new DockBandSettings { ProviderId = "p1", CommandId = "c1" },
                new DockBandSettings { ProviderId = "p2", CommandId = "c2", ShowTitles = true, ShowSubtitles = false }));

        SetupMockLoad(model);
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);

        var start = service.Settings.DockSettings.StartBands;

        // Band 0: migrated from ShowLabels
        Assert.AreEqual(false, start[0].ShowTitles, "Unmigrated band should get ShowTitles from ShowLabels");
        Assert.AreEqual(false, start[0].ShowSubtitles, "Unmigrated band should get ShowSubtitles from ShowLabels");

        // Band 1: already migrated, untouched
        Assert.AreEqual(true, start[1].ShowTitles, "Already-migrated band ShowTitles should be unchanged");
        Assert.AreEqual(false, start[1].ShowSubtitles, "Already-migrated band ShowSubtitles should be unchanged");
    }

    [TestMethod]
    public void Migration_NoDockSettings_NoCrash()
    {
        // Arrange: JSON without DockSettings at all
        var json = """
        {
            "ShowAppDetails": true
        }
        """;
        File.WriteAllText(_settingsFilePath, json);

        var model = CreateMinimalModel();
        SetupMockLoad(model);

        // Act: should not throw
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);

        // Assert: model is usable
        Assert.IsNotNull(service.Settings);
    }

    [TestMethod]
    public void Migration_EmptyBands_NoCrash()
    {
        // Arrange: DockSettings with empty band arrays
        var json = """
        {
            "DockSettings": {
                "StartBands": [],
                "CenterBands": [],
                "EndBands": []
            }
        }
        """;
        File.WriteAllText(_settingsFilePath, json);

        var model = CreateModelWithBands(
            startBands: ImmutableList<DockBandSettings>.Empty,
            centerBands: ImmutableList<DockBandSettings>.Empty,
            endBands: ImmutableList<DockBandSettings>.Empty);

        SetupMockLoad(model);
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);

        Assert.AreEqual(0, service.Settings.DockSettings.StartBands.Count);
    }

    [TestMethod]
    public void Migration_GlobalShowLabels_PreservedAsIs()
    {
        // Arrange: DockSettings-level ShowLabels (NOT per-band) — should not be modified
        var json = """
        {
            "DockSettings": {
                "ShowLabels": false,
                "StartBands": [],
                "CenterBands": [],
                "EndBands": []
            }
        }
        """;
        File.WriteAllText(_settingsFilePath, json);

        var model = CreateModelWithBands(
            startBands: ImmutableList<DockBandSettings>.Empty);
        model = model with
        {
            DockSettings = model.DockSettings with { ShowLabels = false },
        };

        SetupMockLoad(model);
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);

        // Global ShowLabels is preserved (it's NOT [JsonIgnore] at DockSettings level)
        Assert.AreEqual(false, service.Settings.DockSettings.ShowLabels);
    }

    [TestMethod]
    public void Migration_BandContent_PreservedThroughUpgrade()
    {
        // Arrange: bands with various properties
        var json = """
        {
            "DockSettings": {
                "StartBands": [
                    { "ProviderId": "com.microsoft.cmdpal.builtin.core", "CommandId": "com.microsoft.cmdpal.home", "ShowLabels": true },
                    { "ProviderId": "WinGet", "CommandId": "com.microsoft.cmdpal.winget", "ShowLabels": false }
                ],
                "CenterBands": [],
                "EndBands": [
                    { "ProviderId": "PerformanceMonitor", "CommandId": "com.microsoft.cmdpal.performanceWidget" }
                ]
            }
        }
        """;
        File.WriteAllText(_settingsFilePath, json);

        var model = CreateModelWithBands(
            startBands: ImmutableList.Create(
                new DockBandSettings { ProviderId = "com.microsoft.cmdpal.builtin.core", CommandId = "com.microsoft.cmdpal.home" },
                new DockBandSettings { ProviderId = "WinGet", CommandId = "com.microsoft.cmdpal.winget" }),
            endBands: ImmutableList.Create(
                new DockBandSettings { ProviderId = "PerformanceMonitor", CommandId = "com.microsoft.cmdpal.performanceWidget" }));

        SetupMockLoad(model);
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);

        var start = service.Settings.DockSettings.StartBands;
        Assert.AreEqual(2, start.Count);
        Assert.AreEqual("com.microsoft.cmdpal.builtin.core", start[0].ProviderId);
        Assert.AreEqual("com.microsoft.cmdpal.home", start[0].CommandId);
        Assert.AreEqual("WinGet", start[1].ProviderId);
        Assert.AreEqual("com.microsoft.cmdpal.winget", start[1].CommandId);

        var end = service.Settings.DockSettings.EndBands;
        Assert.AreEqual(1, end.Count);
        Assert.AreEqual("PerformanceMonitor", end[0].ProviderId);
    }

    [TestMethod]
    public void Migration_NoSettingsFile_NoCrash()
    {
        // Arrange: no settings.json on disk
        var model = CreateMinimalModel();
        SetupMockLoad(model);

        // Act: should not throw
        var service = new SettingsService(_mockPersistence.Object, _mockAppInfo.Object);

        Assert.IsNotNull(service.Settings);
    }

    // --- Helpers ---
    private void SetupMockLoad(SettingsModel model)
    {
        _mockPersistence
            .Setup(p => p.Load(
                It.IsAny<string>(),
                It.IsAny<System.Text.Json.Serialization.Metadata.JsonTypeInfo<SettingsModel>>()))
            .Returns(model);
    }

    private static SettingsModel CreateMinimalModel()
    {
        var json = "{}";
        return System.Text.Json.JsonSerializer.Deserialize(
            json,
            JsonSerializationContext.Default.SettingsModel) ?? new SettingsModel();
    }

    private static SettingsModel CreateModelWithBands(
        ImmutableList<DockBandSettings>? startBands = null,
        ImmutableList<DockBandSettings>? centerBands = null,
        ImmutableList<DockBandSettings>? endBands = null)
    {
        // Build DockSettings independently to avoid NRE if deserialization yields null DockSettings
        var dockJson = "{}";
        var ds = System.Text.Json.JsonSerializer.Deserialize(
            dockJson,
            JsonSerializationContext.Default.DockSettings) ?? new DockSettings();

        ds = ds with
        {
            StartBands = startBands ?? ImmutableList<DockBandSettings>.Empty,
            CenterBands = centerBands ?? ImmutableList<DockBandSettings>.Empty,
            EndBands = endBands ?? ImmutableList<DockBandSettings>.Empty,
        };

        var model = CreateMinimalModel();
        return model with { DockSettings = ds };
    }
}
