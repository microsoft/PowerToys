// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class ExperimentSettingsTests
{
    [TestMethod]
    public void SandboxBaselineUsesTheProductionSettingsSchema()
    {
        using var stream = typeof(ExperimentSettingsTests).Assembly.GetManifestResourceStream(
            "MouseWithoutBorders.UnitTests.MwbSettings.template.json")
            ?? throw new InvalidOperationException("The Sandbox settings template is missing.");
        var settings = JsonSerializer.Deserialize<MouseWithoutBordersSettings>(stream);

        Assert.IsNotNull(settings);
        Assert.AreEqual("1.1", settings.Version);
        Assert.IsFalse(settings.Properties.UseService);
        Assert.IsFalse(settings.Properties.ShowOriginalUI);
        Assert.IsFalse(settings.Properties.WrapMouse);
        Assert.IsFalse(settings.Properties.ShareClipboard);
        Assert.IsFalse(settings.Properties.TransferFile);
        Assert.AreEqual(string.Empty, settings.Properties.SecurityKey.Value);
        Assert.AreEqual(string.Empty, settings.Properties.Name2IP.Value);
        Assert.AreEqual(0, settings.Properties.MachineMatrixString.Count);
    }
}
