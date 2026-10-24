// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using Awake.Core;
using Awake.Core.Models;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
public class AwakeJsonTests
{
    [TestMethod]
    public void SerializeSettings_PreservesAwakeSettingsContract()
    {
        AwakeSettings settings = new();
        settings.Properties.Mode = AwakeMode.TIMED;
        settings.Properties.IntervalHours = 1;
        settings.Properties.IntervalMinutes = 30;
        settings.Properties.KeepDisplayOn = true;

        string json = AwakeJson.Serialize(settings);
        AwakeSettings roundTripped = JsonSerializer.Deserialize(
            json,
            SettingsSerializationContext.Default.AwakeSettings);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(AwakeMode.TIMED, roundTripped.Properties.Mode);
        Assert.AreEqual(1u, roundTripped.Properties.IntervalHours);
        Assert.AreEqual(30u, roundTripped.Properties.IntervalMinutes);
        Assert.IsTrue(roundTripped.Properties.KeepDisplayOn);
    }

    [TestMethod]
    public void SerializePowerCapabilities_WritesPowerStateNames()
    {
        SystemPowerCapabilities capabilities = new()
        {
            AcOnLineWake = SystemPowerState.PowerSystemWorking,
            SoftLidWake = SystemPowerState.PowerSystemSleeping3,
        };

        string json = AwakeJson.Serialize(capabilities);

        StringAssert.Contains(json, "\"AcOnLineWake\":\"PowerSystemWorking\"");
        StringAssert.Contains(json, "\"SoftLidWake\":\"PowerSystemSleeping3\"");
    }

    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataTestMethod]
    public void SerializeSettings_AllModes_PreserveNumericSettingsValues(int mode)
    {
        AwakeSettings settings = new();
        settings.Properties.Mode = (AwakeMode)mode;

        using JsonDocument document = JsonDocument.Parse(AwakeJson.Serialize(settings));
        Assert.AreEqual(mode, document.RootElement.GetProperty("properties").GetProperty("mode").GetInt32());
    }

    [TestMethod]
    public void SerializeSettings_PreservesExpirationOffsetAndCustomPresets()
    {
        AwakeSettings settings = new()
        {
            Version = "1.2.3",
        };
        settings.Properties.ExpirationDateTime = new DateTimeOffset(2026, 12, 31, 18, 30, 0, TimeSpan.FromHours(-8));
        settings.Properties.CustomTrayTimes["Coffee break"] = 900;
        settings.Properties.CustomTrayTimes["Presentation"] = 5400;

        AwakeSettings roundTripped = JsonSerializer.Deserialize(
            AwakeJson.Serialize(settings),
            SettingsSerializationContext.Default.AwakeSettings);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(settings.Version, roundTripped.Version);
        Assert.AreEqual(settings.Properties.ExpirationDateTime, roundTripped.Properties.ExpirationDateTime);
        Assert.AreEqual(settings.Properties.ExpirationDateTime.Offset, roundTripped.Properties.ExpirationDateTime.Offset);
        CollectionAssert.AreEquivalent(settings.Properties.CustomTrayTimes, roundTripped.Properties.CustomTrayTimes);
    }
}
