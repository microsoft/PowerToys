// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace Microsoft.LightSwitch.UITests;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class LightSwitchHelperTests
{
    [TestMethod]
    public void StopProcessesAttemptsEveryNameBeforeReportingFailures()
    {
        var attempts = new List<string>();
        var error = Assert.ThrowsExactly<AssertFailedException>(() =>
            TestHelper.StopProcesses(name =>
            {
                attempts.Add(name);
                return name == "PowerToys.Settings";
            }));

        CollectionAssert.AreEqual(new[] { "PowerToys", "PowerToys.Settings", TestHelper.ServiceProcess }, attempts);
        StringAssert.Contains(error.Message, "PowerToys, " + TestHelper.ServiceProcess);
    }

    [TestMethod]
    public void RestoreWritesEveryValueAndBroadcastsBeforeReportingMismatches()
    {
        var originals = OriginalThemes();
        var attempts = new List<string>();
        bool broadcast = false;
        var error = Assert.ThrowsExactly<AssertFailedException>(() =>
            TestState.RestoreThemeValues(
                originals,
                (name, _, _) => attempts.Add(name),
                _ => 99,
                () => broadcast = true));

        CollectionAssert.AreEqual(originals.Keys.ToArray(), attempts);
        Assert.IsTrue(broadcast, "A readback mismatch must not suppress the theme notification.");
        foreach (string name in originals.Keys)
        {
            StringAssert.Contains(error.Message, name);
        }
    }

    [TestMethod]
    public void RestoreAttemptsRemainingValuesAndBroadcastAfterARegistryWriteFailure()
    {
        var originals = OriginalThemes();
        var attempts = new List<string>();
        bool broadcast = false;
        var error = Assert.ThrowsExactly<AssertFailedException>(() =>
            TestState.RestoreThemeValues(
                originals,
                (name, _, _) =>
                {
                    attempts.Add(name);
                    if (name == "SystemUsesLightTheme")
                    {
                        throw new UnauthorizedAccessException("Registry access denied.");
                    }
                },
                name => originals[name].Value,
                () => broadcast = true));

        CollectionAssert.AreEqual(originals.Keys.ToArray(), attempts);
        Assert.IsTrue(broadcast);
        StringAssert.Contains(error.Message, "SystemUsesLightTheme: Registry access denied.");
    }

    [TestMethod]
    public void RestorePreservesValuesKindsAndAbsence()
    {
        var originals = OriginalThemes();
        var restored = new Dictionary<string, (object? Value, RegistryValueKind Kind)>();
        bool broadcast = false;
        TestState.RestoreThemeValues(
            originals,
            (name, value, kind) => restored[name] = (value, kind),
            name => restored[name].Value,
            () => broadcast = true);

        Assert.IsTrue(broadcast);
        foreach (var (name, original) in originals)
        {
            Assert.AreEqual(original, restored[name]);
        }
    }

    [TestMethod]
    [DataRow("en-US", "en-US", "", true)]
    [DataRow("en-GB", "en-GB", "en-US", true)]
    [DataRow("fr-FR", "en-US", "", false)]
    [DataRow("en-US", "fr-FR", "", false)]
    [DataRow("en-US", "en-US", "fr-FR", false)]
    public void LanguagePrerequisitesAreExplicit(string ui, string format, string settingsLanguage, bool supported)
    {
        Action check = () => TestHelper.AssertLanguagePrerequisites(
            CultureInfo.GetCultureInfo(ui),
            CultureInfo.GetCultureInfo(format),
            settingsLanguage);
        if (supported)
        {
            check();
        }
        else
        {
            var error = Assert.ThrowsExactly<AssertFailedException>(check);
            StringAssert.Contains(error.Message, "English");
        }
    }

    private static Dictionary<string, (object? Value, RegistryValueKind Kind)> OriginalThemes() => new()
    {
        ["SystemUsesLightTheme"] = (1, RegistryValueKind.DWord),
        ["AppsUseLightTheme"] = (0, RegistryValueKind.DWord),
        ["ColorPrevalence"] = (null, RegistryValueKind.DWord),
    };
}
