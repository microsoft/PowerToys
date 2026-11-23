// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class DockMaterialPersistenceTests
{
    [TestMethod]
    [DataRow(DockBackdrop.Transparent, "Transparent")]
    [DataRow(DockBackdrop.Acrylic, "Acrylic")]
    [DataRow(DockBackdrop.Mica, "Mica")]
    [DataRow(DockBackdrop.MicaAlt, "MicaAlt")]
    public void SaveAndReload_PreservesDockMaterialIndependentlyOfPalette(DockBackdrop material, string persistedName)
    {
        var settings = new SettingsModel
        {
            BackdropStyle = BackdropStyle.MicaAlt,
            DockSettings = new DockSettings { Backdrop = material },
        };

        var json = JsonSerializer.Serialize(settings, JsonSerializationContext.Default.SettingsModel);
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual(persistedName, document.RootElement.GetProperty("DockSettings").GetProperty("Backdrop").GetString());

        var reloaded = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual(material, reloaded.DockSettings.Backdrop);
        Assert.AreEqual(BackdropStyle.MicaAlt, reloaded.BackdropStyle, "Changing the Dock material must not change the palette material.");
    }
}
