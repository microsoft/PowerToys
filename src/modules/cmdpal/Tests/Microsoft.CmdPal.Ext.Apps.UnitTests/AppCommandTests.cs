// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WyHash;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppCommandTests
{
    [TestMethod]
    public void PackagedAppCommandIdUsesUserModelIdRegardlessOfDisplayMetadata()
    {
        var app = new AppItem
        {
            IsPackaged = true,
            UserModelId = "Microsoft.ScreenSketch_8wekyb3d8bbwe!App",
            Name = "Snipping Tool",
            Subtitle = "Packaged application",
            ExePath = "C:\\Original",
        };
        var original = new AppListItem(app, useThumbnails: false);

        app.Name = "Localized Snipping Tool";
        app.Subtitle = "Localized application type";
        app.ExePath = "C:\\Updated";
        var updated = new AppListItem(app, useThumbnails: false);

        Assert.AreEqual(app.UserModelId, original.Command.Id);
        Assert.AreEqual(original.Command.Id, updated.Command.Id);
    }

    [TestMethod]
    public void UnpackagedAppCommandIdKeepsExistingHashFormat()
    {
        var app = new AppItem
        {
            Name = "TestApp",
            Subtitle = "Desktop application",
            ExePath = "C:\\TestApp.exe",
            UserModelId = "TestApp.Desktop",
        };
        var item = new AppListItem(app, useThumbnails: false);
        var hash = WyHash64.ComputeHash64(app.Name + app.Subtitle + app.ExePath, seed: 0);

        Assert.AreEqual($"{app.Name}_{hash}", item.Command.Id);
    }
}
