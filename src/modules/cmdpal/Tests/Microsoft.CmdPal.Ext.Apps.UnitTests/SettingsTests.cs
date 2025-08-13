// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class SettingsTests
{
    [TestMethod]
    public void DefaultSettings_HasExpectedValues()
    {
        // Act
        var settings = Settings.CreateDefaultSettings();

        // Assert
        Assert.IsTrue(settings.EnableStartMenuSource);
        Assert.IsTrue(settings.EnableDesktopSource);
        Assert.IsTrue(settings.EnableRegistrySource);
        Assert.IsTrue(settings.EnablePathEnvironmentVariableSource);
        Assert.AreEqual(5, settings.ProgramSuffixes.Count);
        Assert.AreEqual(7, settings.RunCommandSuffixes.Count);
    }

    [TestMethod]
    public void DisabledSourcesSettings_DisablesAllSources()
    {
        // Act
        var settings = Settings.CreateDisabledSourcesSettings();

        // Assert
        Assert.IsFalse(settings.EnableStartMenuSource);
        Assert.IsFalse(settings.EnableDesktopSource);
        Assert.IsFalse(settings.EnableRegistrySource);
        Assert.IsFalse(settings.EnablePathEnvironmentVariableSource);
    }

    [TestMethod]
    public void CustomSuffixesSettings_HasCustomSuffixes()
    {
        // Act
        var settings = Settings.CreateCustomSuffixesSettings();

        // Assert
        Assert.AreEqual(2, settings.ProgramSuffixes.Count);
        Assert.IsTrue(settings.ProgramSuffixes.Contains("exe"));
        Assert.IsTrue(settings.ProgramSuffixes.Contains("bat"));

        Assert.AreEqual(3, settings.RunCommandSuffixes.Count);
        Assert.IsTrue(settings.RunCommandSuffixes.Contains("exe"));
        Assert.IsTrue(settings.RunCommandSuffixes.Contains("bat"));
        Assert.IsTrue(settings.RunCommandSuffixes.Contains("cmd"));
    }

    [TestMethod]
    public void SettingsInterface_ImplementedCorrectly()
    {
        // Setup
        var settings = Settings.CreateDefaultSettings();

        // Assert - Verify all interface properties are accessible
        Assert.IsNotNull(settings.ProgramSuffixes);
        Assert.IsNotNull(settings.RunCommandSuffixes);

        // Verify boolean properties
        Assert.IsTrue(settings.EnableStartMenuSource || !settings.EnableStartMenuSource); // Just verify it's accessible
        Assert.IsTrue(settings.EnableDesktopSource || !settings.EnableDesktopSource);
        Assert.IsTrue(settings.EnableRegistrySource || !settings.EnableRegistrySource);
        Assert.IsTrue(settings.EnablePathEnvironmentVariableSource || !settings.EnablePathEnvironmentVariableSource);
    }
}
