// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Contracts;
using PowerDisplay.Ipc;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc.UnitTests;

/// <summary>
/// Invariant guards for <see cref="CliSettingCatalog"/> — the single source of per-setting VCP
/// metadata. These pin the catalog's shape so the read/write call sites that consume it cannot
/// silently drift.
/// </summary>
[TestClass]
public class CliSettingCatalogTests
{
    [TestMethod]
    public void Catalog_CoversTheSixVcpSettings_InCanonicalOrder()
    {
        var names = CliSettingCatalog.VcpSettings.Select(s => s.Name).ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                CliSettingNames.Brightness,
                CliSettingNames.Contrast,
                CliSettingNames.Volume,
                CliSettingNames.ColorTemperature,
                CliSettingNames.InputSource,
                CliSettingNames.PowerState,
            },
            names);
    }

    [TestMethod]
    public void Catalog_ExcludesOrientation()
    {
        // Orientation is GDI-based, not a VCP setting, so it must not be in the VCP catalog.
        Assert.IsNull(CliSettingCatalog.TryGet(CliSettingNames.Orientation));
        Assert.IsFalse(CliSettingCatalog.VcpSettings.Any(s => s.Name == CliSettingNames.Orientation));
    }

    [TestMethod]
    public void Catalog_TryGet_ReturnsNullForUnknownName()
    {
        Assert.IsNull(CliSettingCatalog.TryGet("does-not-exist"));
    }

    [TestMethod]
    public void Catalog_ClassifiesContinuousAndDiscreteSettings()
    {
        Assert.AreEqual(CliSettingKind.Continuous, CliSettingCatalog.TryGet(CliSettingNames.Brightness)!.Kind);
        Assert.AreEqual(CliSettingKind.Continuous, CliSettingCatalog.TryGet(CliSettingNames.Contrast)!.Kind);
        Assert.AreEqual(CliSettingKind.Continuous, CliSettingCatalog.TryGet(CliSettingNames.Volume)!.Kind);
        Assert.AreEqual(CliSettingKind.Discrete, CliSettingCatalog.TryGet(CliSettingNames.ColorTemperature)!.Kind);
        Assert.AreEqual(CliSettingKind.Discrete, CliSettingCatalog.TryGet(CliSettingNames.InputSource)!.Kind);
        Assert.AreEqual(CliSettingKind.Discrete, CliSettingCatalog.TryGet(CliSettingNames.PowerState)!.Kind);
    }

    [TestMethod]
    public void Catalog_OnlyPowerStateBlanksDisplay()
    {
        // Only power-state can blank the panel, so it is the only setting that gates --confirm-power-off.
        Assert.IsTrue(CliSettingCatalog.TryGet(CliSettingNames.PowerState)!.BlanksDisplay);
        foreach (var setting in CliSettingCatalog.VcpSettings.Where(s => s.Name != CliSettingNames.PowerState))
        {
            Assert.IsFalse(setting.BlanksDisplay, $"{setting.Name} must not blank the display");
        }
    }

    [TestMethod]
    public void Catalog_ContinuousSettingsHaveNoDiscreteSupportedValues()
    {
        var monitor = new Monitor();
        foreach (var setting in CliSettingCatalog.VcpSettings.Where(s => s.Kind == CliSettingKind.Continuous))
        {
            Assert.IsNull(setting.SupportedValues(monitor), $"{setting.Name} is continuous and has no discrete value set");
        }
    }
}
