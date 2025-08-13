// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.DSC.Commands;
using PowerToys.DSC.DSCResources;
using PowerToys.DSC.Models.ResourceObjects;

namespace PowerToys.DSC.UnitTests.SettingsResourceTests;

[TestClass]
public sealed class SettingsResourceAwakeModuleTest : SettingsResourceModuleTest<AwakeSettings>
{
    private List<string> DiffSettings { get; } = [SettingsResourceObject<AwakeSettings>.SettingsJsonPropertyName];

    private List<string> DiffEmpty { get; } = [];

    public SettingsResourceAwakeModuleTest()
        : base(nameof(ModuleType.Awake))
    {
    }

    [TestMethod]
    public void SetWithDiff_Success()
    {
        // Arrange
        ResetSettingsToDefaultValues();

        // Arrange
        uint expectedIntervalHours = DefaultSettings.Properties.IntervalHours + 1;
        var input = CreateResourceObject(new AwakeSettings
        {
            Properties = new AwakeProperties
            {
                IntervalHours = expectedIntervalHours,
            },
        });

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<SettingsResourceObject<AwakeSettings>>();

        // Assert
        Assert.IsTrue(result.Success);
        AssertSettingsHasChanged(expectedIntervalHours);
        AssertStateAndSettingsAreEqual(GetSettings(), state);
        CollectionAssert.AreEqual(DiffSettings, diff);
    }

    [TestMethod]
    public void SetWithoutDiff_Success()
    {
        // Arrange
        ResetSettingsToDefaultValues();
        uint expectedIntervalHours = DefaultSettings.Properties.IntervalHours + 1;
        UpdateSettings(expectedIntervalHours);
        var currentSettingsBeforeExecute = GetSettings();
        var input = CreateResourceObject(new AwakeSettings
        {
            Properties = new AwakeProperties
            {
                ExpirationDateTime = currentSettingsBeforeExecute.Properties.ExpirationDateTime,
                IntervalHours = expectedIntervalHours,
            },
        });

        // Act
        var result = ExecuteDscCommand<SetCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<SettingsResourceObject<AwakeSettings>>();

        // Assert
        Assert.IsTrue(result.Success);
        AssertSettingsHasChanged(expectedIntervalHours);
        AssertStateAndSettingsAreEqual(currentSettingsBeforeExecute, state);
        CollectionAssert.AreEqual(DiffEmpty, diff);
    }

    [TestMethod]
    public void TestWithDiff_Success()
    {
        // Arrange
        ResetSettingsToDefaultValues();
        uint expectedIntervalHours = 2;
        var input = CreateResourceObject(new AwakeSettings
        {
            Properties = new AwakeProperties
            {
                IntervalHours = expectedIntervalHours,
            },
        });

        // Act
        var result = ExecuteDscCommand<TestCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<SettingsResourceObject<AwakeSettings>>();

        // Assert
        Assert.IsTrue(result.Success);
        AssertSettingsAreEqual(DefaultSettings, GetSettings());
        AssertStateAndSettingsAreEqual(DefaultSettings, state);
        CollectionAssert.AreEqual(DiffSettings, diff);
        Assert.IsFalse(state.InDesiredState);
    }

    [TestMethod]
    public void TestWithoutDiff_Success()
    {
        // Arrange
        ResetSettingsToDefaultValues();
        uint expectedIntervalHours = 2;
        UpdateSettings(expectedIntervalHours);
        var currentSettings = GetSettings();
        var input = CreateResourceObject(new AwakeSettings
        {
            Properties = new AwakeProperties
            {
                ExpirationDateTime = currentSettings.Properties.ExpirationDateTime,
                IntervalHours = expectedIntervalHours,
            },
        });

        // Act
        var result = ExecuteDscCommand<TestCommand>("--resource", SettingsResource.ResourceName, "--module", Module, "--input", input);
        var (state, diff) = result.OutputStateAndDiff<SettingsResourceObject<AwakeSettings>>();

        // Assert
        Assert.IsTrue(result.Success);
        AssertStateAndSettingsAreEqual(GetSettings(), state);
        CollectionAssert.AreEqual(DiffEmpty, diff);
        Assert.IsTrue(state.InDesiredState);
    }

    /// <summary>
    /// Asserts that the current settings have changed.
    /// </summary>
    /// <param name="intervalHours">The expected interval hours.</param>
    private void AssertSettingsHasChanged(uint intervalHours)
    {
        var currentSettings = GetSettings();
        var defaultSettings = DefaultSettings;

        defaultSettings.Properties.IntervalHours = intervalHours;

        AssertSettingsAreEqual(defaultSettings, currentSettings);
    }

    /// <summary>
    /// Updates the settings with the provided interval hours.
    /// </summary>
    /// <param name="intervalHours">The interval hours to set.</param>
    private void UpdateSettings(uint intervalHours)
    {
        var settings = GetSettings();
        settings.Properties.IntervalHours = intervalHours;
        SaveSettings(settings);
    }

    /// <inheritdoc/>
    protected override void AssertSettingsAreEqual(AwakeSettings expected, AwakeSettings actual)
    {
        // Don't compare ExpirationDateTime as it is set to the current time during initialization
        expected.Properties.ExpirationDateTime = actual.Properties.ExpirationDateTime;

        base.AssertSettingsAreEqual(expected, actual);
    }
}
