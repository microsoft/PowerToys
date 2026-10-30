// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CmdPal.Ext.Apps.Catalog.Payloads;
using Microsoft.CmdPal.Ext.Apps.Win32;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppPayloadTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ToAppItem_OpenInConsoleUsesAppDirectory(bool packaged)
    {
        const string directory = @"C:\Apps\Containing folder";
        IAppCatalogPayload payload = packaged
            ? new PackagedAppPayload
            {
                Name = "App",
                AppUserModelId = "Contoso.App_123!app",
                PackageLocation = directory,
                IsNonRemovable = true,
            }
            : new Win32AppPayload
            {
                Name = "App",
                TargetPath = @"C:\Bin\App.exe",
                LnkFilePath = Path.Combine(directory, "App.lnk"),
                ParentDirectory = directory,
                AppType = Win32AppType.ShortcutApplication,
            };
        var console = payload.ToAppItem().Commands.OfType<CommandContextItem>()
            .Select(context => context.Command).OfType<OpenInConsoleCommand>().Single();

        // Inspect the configured directory without starting a terminal during the test.
        var pathField = typeof(OpenInConsoleCommand).GetField("_path", BindingFlags.Instance | BindingFlags.NonPublic);
        var directoryField = typeof(OpenInConsoleCommand).GetField("_isDirectory", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(pathField, "Update this assertion if the toolkit's path storage changes.");
        Assert.IsNotNull(directoryField, "Update this assertion if the toolkit's directory flag changes.");
        Assert.AreEqual(directory, pathField.GetValue(console));
        Assert.AreEqual(true, directoryField.GetValue(console), "The command must open the supplied directory, not its parent.");
    }
}
