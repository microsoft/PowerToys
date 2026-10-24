// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CmdPal.Ext.Apps.AppList;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Payloads;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Packaged;
using Microsoft.CmdPal.Ext.Apps.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

public partial class PackagedAppMetadataTests
{
    [STATestMethod]
    public void ReadManifest_EdgePwaRetainsLaunchMetadataWithoutChangingActivation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-edge-pwa-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "AppxManifest.xml"), CreateEdgePwaManifest().ToString());
            var package = TestDataHelper.CreateTestPackagedMetadata(packageLocation: root).Package;

            var apps = PackagedAppReader.ReadManifest(package, out var complete);

            Assert.IsTrue(complete);
            var app = apps.Single();
            Assert.AreEqual(TestDataHelper.CreateEdgePwaLaunchInfo(), app.EdgePwaLaunch);
            var item = PackagedAppSource.CreateCatalogItem(app);
            Assert.AreEqual(app.EdgePwaLaunch, ((PackagedAppPayload)item.Payload).EdgePwaLaunch);
            Assert.AreEqual(AppIdentity.ForPackaged(app.AppUserModelId), item.Identity);
            Assert.AreEqual(app.AppUserModelId, item.ToAppItem().AppUserModelId);
            Assert.IsTrue(item.ToAppItem().IsWebApp);
            Assert.IsTrue(item.ToAppItem().IsPackaged);
            Assert.IsNull(item.Payload.GetCanonicalTargetPath());
            using var writer = File.Open(Path.Combine(root, "AppxManifest.xml"), FileMode.Open, FileAccess.Write, FileShare.None);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [STATestMethod]
    [DataRow("com.ms.webapp.internals.3", "PWA", true)]
    [DataRow(null, "PWA", true)]
    [DataRow("com.ms.webapp.internals.3", "OtherHost", false)]
    public void ReadManifest_WebAppClassificationDoesNotRequireRecognizedEdgeLaunch(string? extensionName, string hostId, bool webApp)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-web-app-host-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var manifestPath = Path.Combine(root, "AppxManifest.xml");
        try
        {
            var manifest = CreateEdgePwaManifest();
            var ns = manifest.Root!.Name.Namespace;
            XNamespace uap10 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/10";
            var application = manifest.Root.Element(ns + "Applications")!.Elements().Single();
            application.SetAttributeValue(uap10 + "HostId", hostId);
            var extensions = application.Element(ns + "Extensions")!;
            if (extensionName is null)
            {
                extensions.Remove();
            }
            else
            {
                extensions.Elements().Single().Elements().Single().SetAttributeValue("Name", extensionName);
            }

            File.WriteAllText(manifestPath, manifest.ToString());
            var package = TestDataHelper.CreateTestPackagedMetadata(packageLocation: root).Package;

            var apps = PackagedAppReader.ReadManifest(package, out var complete);

            Assert.IsTrue(complete);
            var metadata = apps.Single();
            Assert.AreEqual(webApp, metadata.IsWebApp);
            Assert.IsNull(metadata.EdgePwaLaunch);
            var item = PackagedAppSource.CreateCatalogItem(metadata);
            var app = item.ToAppItem();
            Assert.AreEqual(webApp, app.IsWebApp);
            Assert.AreEqual(AppIdentity.ForPackaged(metadata.AppUserModelId), item.Identity);
            Assert.AreEqual(metadata.AppUserModelId, app.AppUserModelId);
            var rows = AllAppsPage.FilterAppItems([new AppListItem(app)], AllAppsFilters.WebFilterId, search: null);
            Assert.AreEqual(webApp ? 1 : 0, rows.Length);
        }
        finally
        {
            File.Delete(manifestPath);
            Directory.Delete(root);
        }
    }

    [TestMethod]
    [DataRow("missing-host")]
    [DataRow("multiple-hosts")]
    [DataRow("missing-extension")]
    [DataRow("multiple-extensions")]
    [DataRow("unknown-extension")]
    [DataRow("wrong-extension-category")]
    [DataRow("missing-publisher")]
    [DataRow("missing-host-publisher")]
    [DataRow("unknown-host")]
    [DataRow("missing-profile")]
    [DataRow("empty-profile")]
    [DataRow("conflicting-parameters")]
    [DataRow("empty-app-id")]
    [DataRow("executable")]
    public void EdgePwaLaunchInfo_IncompleteOrUnfamiliarManifestIsNotAssociated(string change)
    {
        var manifest = CreateEdgePwaManifest();
        var root = manifest.Root!;
        var ns = root.Name.Namespace;
        XNamespace uap10 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/10";
        var application = root.Element(ns + "Applications")!.Elements().Single();
        var host = root.Element(ns + "Dependencies")!.Element(uap10 + "HostRuntimeDependency")!;
        var extension = application.Element(ns + "Extensions")!.Elements().Single();
        var info = extension.Elements().Single();
        switch (change)
        {
            case "missing-host":
                host.Remove();
                break;
            case "multiple-hosts":
                host.AddAfterSelf(new XElement(host));
                break;
            case "missing-extension":
                extension.Remove();
                break;
            case "multiple-extensions":
                extension.AddAfterSelf(new XElement(extension));
                break;
            case "unknown-extension":
                info.SetAttributeValue("Name", "com.ms.webapp.internals.3");
                break;
            case "wrong-extension-category":
                extension.SetAttributeValue("Category", "windows.protocol");
                break;
            case "missing-publisher":
                root.Element(ns + "Identity")!.Attribute("Publisher")!.Remove();
                break;
            case "missing-host-publisher":
                host.Attribute("Publisher")!.Remove();
                break;
            case "unknown-host":
                host.SetAttributeValue("Name", "Contoso.Browser");
                break;
            case "missing-profile":
                info.SetAttributeValue("Description", $"parameters?{(string?)application.Attribute(uap10 + "Parameters")}");
                break;
            case "empty-profile":
                info.SetAttributeValue("Description", ((string?)info.Attribute("Description"))!.Replace("?Default;", "?;", StringComparison.Ordinal));
                break;
            case "conflicting-parameters":
                application.SetAttributeValue(uap10 + "Parameters", "--app-id=different");
                break;
            case "empty-app-id":
                application.SetAttributeValue(uap10 + "Parameters", "--app-id=");
                info.SetAttributeValue("Description", "parameters?--app-id=;profile-directory?Default");
                break;
            case "executable":
                application.SetAttributeValue("Executable", "host.exe");
                break;
        }

        Assert.IsNull(PackagedAppReader.ReadEdgePwaLaunchInfo(application));
    }

    [TestMethod]
    public void EdgePwaLaunchInfo_IgnoresGeneratedIdsAndKeepsCompleteLaunchContext()
    {
        var manifest = CreateEdgePwaManifest();
        var root = manifest.Root!;
        var ns = root.Name.Namespace;
        var application = root.Element(ns + "Applications")!.Elements().Single();
        var extension = application.Element(ns + "Extensions")!.Elements().Single().Elements().Single();
        var first = PackagedAppReader.ReadEdgePwaLaunchInfo(application);
        root.Element(ns + "Identity")!.SetAttributeValue("Name", "www.youtube.com-another");
        root.Element(ns + "Identity")!.SetAttributeValue("Version", "2.0.0.0");
        extension.SetAttributeValue("Id", "www.youtube.com-another");

        Assert.IsNotNull(first);
        Assert.AreEqual(first, PackagedAppReader.ReadEdgePwaLaunchInfo(application));
        extension.SetAttributeValue("Description", first.LaunchContext + "&extra=a;b");
        var changed = PackagedAppReader.ReadEdgePwaLaunchInfo(application);
        Assert.IsNotNull(changed);
        Assert.AreEqual(first.LaunchContext + "&extra=a;b", changed.LaunchContext);
        Assert.AreNotEqual(first, changed);
    }

    private static XDocument CreateEdgePwaManifest()
    {
        var manifest = XDocument.Parse(CreateManifest(string.Empty));
        var root = manifest.Root!;
        var ns = root.Name.Namespace;
        XNamespace uap3 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/3";
        XNamespace uap10 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/10";
        var info = TestDataHelper.CreateEdgePwaLaunchInfo();

        // Hosted apps require the uap10 namespace, so the native reader must not ignore it.
        root.SetAttributeValue("IgnorableNamespaces", "uap uap3 uap5 uap8 desktop");
        var deviceFamily = root.Element(ns + "Dependencies")!.Element(ns + "TargetDeviceFamily")!;
        deviceFamily.SetAttributeValue("MinVersion", "10.0.19041.0");
        deviceFamily.SetAttributeValue("MaxVersionTested", "10.0.19041.0");
        root.Element(ns + "Identity")!.SetAttributeValue("Publisher", info.PackagePublisher);
        root.Element(ns + "Dependencies")!.Add(new XElement(
            uap10 + "HostRuntimeDependency",
            new XAttribute("Name", info.HostPackageName),
            new XAttribute("Publisher", info.HostPackagePublisher),
            new XAttribute("MinVersion", "1.0.0.0")));
        var application = root.Element(ns + "Applications")!.Elements().Single();
        application.Attribute("StartPage")!.Remove();
        application.SetAttributeValue(uap10 + "HostId", info.HostId);
        application.SetAttributeValue(uap10 + "Parameters", info.Parameters);
        application.Add(new XElement(
            ns + "Extensions",
            new XElement(
                uap3 + "Extension",
                new XAttribute("Category", "windows.appExtension"),
                new XElement(
                    uap3 + "AppExtension",
                    new XAttribute("Name", "com.ms.webapp.internals.2"),
                    new XAttribute("Id", "www.youtube.com-generated"),
                    new XAttribute("PublicFolder", "Public"),
                    new XAttribute("DisplayName", "Edge WebApp Internals"),
                    new XAttribute("Description", info.LaunchContext)))));
        return manifest;
    }
}
