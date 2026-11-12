// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class PowerScriptsManifestTests
{
    private static readonly string[] SharedDirectories = [@"$(KnownFolder:LocalAppData)\Microsoft\PowerToys\PowerScripts"];

    [TestMethod]
    [DataRow("Package.appxmanifest")]
    [DataRow("Package-Dev.appxmanifest")]
    public void PackageSharesOnlyThePowerScriptsModuleData(string manifestName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Manifests", manifestName);
        var manifest = XDocument.Load(path);
        XNamespace package = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        XNamespace virtualization = "http://schemas.microsoft.com/appx/manifest/virtualization/windows10";
        XNamespace desktop6 = "http://schemas.microsoft.com/appx/manifest/desktop/windows10/6";
        XNamespace restricted = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";
        var properties = manifest.Root!.Element(package + "Properties")!;
        var sharedDirectories = properties
            .Element(virtualization + "FileSystemWriteVirtualization")!
            .Element(virtualization + "ExcludedDirectories")!
            .Elements(virtualization + "ExcludedDirectory")
            .Select(element => element.Value)
            .ToArray();

        CollectionAssert.AreEqual(SharedDirectories, sharedDirectories);
        Assert.IsNull(properties.Element(desktop6 + "FileSystemWriteVirtualization"));
        Assert.IsTrue(manifest.Root
            .Element(package + "Capabilities")!
            .Elements(restricted + "Capability")
            .Any(capability => (string?)capability.Attribute("Name") == "unvirtualizedResources"));
    }
}
