// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.System.Variant;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppUserModelIdTests
{
    [TestMethod]
    [DataRow("", false)]
    [DataRow("Contoso.Console", false)]
    [DataRow("Contoso.Console_123!App", false)]
    [DataRow("Contoso.Console", true)]
    [DataRow("Contoso.Konzole.Žluťoučký", true)]
    public void Shortcut_ExplicitIdRetainsMetadataWithoutPackagedIdentity(string explicitId, bool useLoadedLink)
    {
        var shortcutPath = Path.Combine(Path.GetTempPath(), $"CmdPal-AppID-{Guid.NewGuid():N}.lnk");
        var targetPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var iconLocation = $"{Path.Combine(Environment.SystemDirectory, "shell32.dll")},-4";
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        try
        {
            shortcut.TargetPath = targetPath;
            shortcut.Arguments = "/c echo explicit-app-id";
            shortcut.WorkingDirectory = Environment.SystemDirectory;
            shortcut.Description = "Explicit AppID shortcut";
            shortcut.IconLocation = iconLocation;
            shortcut.Save();
            if (explicitId.Length != 0)
            {
                if (useLoadedLink)
                {
                    SetExplicitAppUserModelIdOnLoadedLink(shortcutPath, explicitId);
                }
                else
                {
                    SetExplicitAppUserModelId(shortcutPath, explicitId);
                }
            }

            var link = ShellLinkReader.Read(shortcutPath);
            Assert.IsNotNull(link);
            Assert.AreEqual(explicitId, link.ExplicitAppUserModelId);
            Assert.AreEqual(string.Empty, link.PackagedAppUserModelId);
            Assert.IsTrue(string.Equals(targetPath, link.TargetPath, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual("/c echo explicit-app-id", link.Arguments);
            Assert.AreEqual(Environment.SystemDirectory, link.WorkingDirectory);
            Assert.AreEqual("Explicit AppID shortcut", link.Description);
            Assert.AreEqual(iconLocation, link.IconLocation);

            var program = Win32AppReader.LoadFromPath(shortcutPath, asRunCommand: false);
            Assert.IsTrue(program.Valid);
            Assert.AreEqual(explicitId, program.ExplicitAppUserModelId);
            Assert.AreEqual(string.Empty, program.PackagedAppUserModelId);
            Assert.IsTrue(string.Equals(targetPath, program.TargetPath, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(shortcutPath, program.LnkFilePath);

            var payload = Win32AppPayload.From(program);
            Assert.AreEqual(explicitId, payload.ExplicitAppUserModelId);
            Assert.AreEqual(link.Arguments, payload.Arguments);
            Assert.AreEqual(link.WorkingDirectory, payload.WorkingDirectory);
            Assert.AreEqual(link.Description, payload.Description);
            Assert.AreEqual(link.IconLocation, payload.IconLocation);
            Assert.IsNull(((IAppCatalogPayload)payload).GetCanonicalIdentityHint());
            var app = payload.ToAppItem();
            Assert.AreEqual(explicitId, app.AppUserModelId);
            Assert.IsFalse(app.IsPackaged);
            Assert.AreEqual(shortcutPath, app.LaunchTarget);
            Assert.AreEqual(program.TargetPath, app.ResolvedTarget);
            Assert.AreEqual(link.Arguments, app.LaunchArguments);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
            File.Delete(shortcutPath);
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public async Task Cache_ExplicitIdRoundTripsAndOlderPayloadDefaultsToEmpty(bool omitExplicitId, bool hasPackagedId)
    {
        var cachePath = Path.Combine(Path.GetTempPath(), $"CmdPal-AppID-{Guid.NewGuid():N}.json");
        try
        {
            var program = CreateProgram(@"C:\Links\Console.lnk");
            program.ExplicitAppUserModelId = "Contoso.Console";
            program.PackagedAppUserModelId = hasPackagedId ? "Contoso.Package_123!App" : string.Empty;
            program.Arguments = "--profile cached";
            program.WorkingDirectory = @"C:\Projects\Cached";
            var item = new AppCatalogItem(
                "win32:cached",
                priority: 0,
                new AppCatalogSourceReference("test", program.LnkFilePath),
                [program.ExplicitAppUserModelId],
                Win32AppPayload.From(program));
            var context = new AppCatalogCacheContext
            {
                Language = "en-US",
                NowUtc = DateTimeOffset.UtcNow,
                SourceKeys = new Dictionary<string, string> { ["win32:test"] = "test-key" },
            };
            var cache = new AppCatalogCache(cachePath);
            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>> { ["win32:test"] = [item] },
                ["win32:test"],
                context,
                CancellationToken.None);
            if (omitExplicitId)
            {
                var json = JsonNode.Parse(File.ReadAllText(cachePath));
                var payloadJson = json["Sources"][0]["Items"][0]["Payload"].AsObject();
                Assert.IsTrue(payloadJson.Remove(nameof(Win32AppPayload.ExplicitAppUserModelId)));
                File.WriteAllText(cachePath, json.ToJsonString());
            }

            var loaded = await cache.LoadAsync(context, CancellationToken.None);
            Assert.IsNotNull(loaded);
            var loadedItem = loaded.Sources.Single().Items.Single();
            var payload = loadedItem.Payload as Win32AppPayload;
            Assert.IsNotNull(payload);
            var expectedExplicitId = omitExplicitId ? string.Empty : program.ExplicitAppUserModelId;
            Assert.AreEqual(expectedExplicitId, payload.ExplicitAppUserModelId);
            Assert.AreEqual(program.PackagedAppUserModelId, payload.PackagedAppUserModelId);
            Assert.AreEqual(program.TargetPath, payload.TargetPath);
            Assert.AreEqual(program.LnkFilePath, payload.LnkFilePath);
            Assert.AreEqual(program.Arguments, payload.Arguments);
            Assert.AreEqual(program.WorkingDirectory, payload.WorkingDirectory);
            Assert.AreEqual(item.Identity, loadedItem.Identity);
            CollectionAssert.AreEqual(item.CommandIds.ToArray(), loadedItem.CommandIds.ToArray());
            CollectionAssert.AreEqual(item.IdentityAliases.ToArray(), loadedItem.IdentityAliases.ToArray());
            var app = loadedItem.ToAppItem();
            Assert.AreEqual(program.Arguments, app.LaunchArguments);
            Assert.AreEqual(hasPackagedId ? program.PackagedAppUserModelId : expectedExplicitId, app.AppUserModelId);
            Assert.IsFalse(app.IsPackaged);
            Assert.AreEqual(program.LnkFilePath, app.LaunchTarget);
            Assert.AreEqual(new AppCommand(item.ToAppItem()).Id, new AppCommand(app).Id);
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

    [TestMethod]
    public async Task Source_ExplicitIdEnrichesSearchWithoutChangingIdentities()
    {
        var program = CreateProgram(@"C:\Links\Console.lnk");
        var original = (await LoadItemsAsync(program)).Single();
        foreach (var explicitId in new[] { "Contoso.Console", "Contoso.Console_123!App" })
        {
            program.ExplicitAppUserModelId = explicitId;
            var enriched = (await LoadItemsAsync(program)).Single();

            Assert.AreEqual(original.Identity, enriched.Identity);
            CollectionAssert.AreEqual(original.IdentityAliases.ToArray(), enriched.IdentityAliases.ToArray());
            CollectionAssert.AreEqual(original.ToAppItem().CommandIds.ToArray(), enriched.ToAppItem().CommandIds.ToArray());
            Assert.AreEqual(new AppCommand(original.ToAppItem()).Id, new AppCommand(enriched.ToAppItem()).Id);
            Assert.IsNull(enriched.Payload.GetCanonicalIdentityHint());
            Assert.AreEqual(original.Payload.GetCanonicalTargetPath(), enriched.Payload.GetCanonicalTargetPath());
            CollectionAssert.Contains(enriched.MatchTerms.ToArray(), explicitId);
            Assert.AreEqual(explicitId, enriched.ToAppItem().AppUserModelId);
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Source_SharedExplicitIdKeepsDifferentLaunchProfiles(bool differentArguments)
    {
        var first = CreateProgram(@"C:\Links\First.lnk");
        var second = CreateProgram(@"C:\Links\Second.lnk");
        first.ExplicitAppUserModelId = second.ExplicitAppUserModelId = "Contoso.Console";
        second.Arguments = differentArguments ? "--profile second" : string.Empty;
        second.WorkingDirectory = differentArguments ? string.Empty : @"C:\Projects\Second";

        var items = await LoadItemsAsync(first, second);

        Assert.AreEqual(2, items.Count);
        Assert.AreEqual(2, items.Select(item => item.Identity).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.IsTrue(items.All(item => item.Payload.GetCanonicalIdentityHint() is null));
    }

    private static unsafe void SetExplicitAppUserModelId(string path, string explicitId)
    {
        PInvoke.SHGetPropertyStoreFromParsingName(
            path,
            null,
            GETPROPERTYSTOREFLAGS.GPS_READWRITE,
            typeof(IPropertyStore).GUID,
            out var propertyStoreObject).ThrowOnFailure();
        var propertyStore = (IPropertyStore*)propertyStoreObject;
        using var handle = new SafeComHandle((IntPtr)propertyStore);
        SetExplicitAppUserModelId(propertyStore, explicitId);
    }

    private static unsafe void SetExplicitAppUserModelIdOnLoadedLink(string path, string explicitId)
    {
        IShellLinkW* link = null;
        PInvoke.CoCreateInstance(typeof(ShellLink).GUID, null, CLSCTX.CLSCTX_INPROC_SERVER, out link).ThrowOnFailure();
        using var linkHandle = new SafeComHandle((IntPtr)link);

        IPersistFile* persistFile = null;
        var iid = typeof(IPersistFile).GUID;
        ((IUnknown*)link)->QueryInterface(&iid, (void**)&persistFile).ThrowOnFailure();
        using var persistFileHandle = new SafeComHandle((IntPtr)persistFile);
        persistFile->Load(path, STGM.STGM_READWRITE).ThrowOnFailure();

        IPropertyStore* propertyStore = null;
        iid = typeof(IPropertyStore).GUID;
        ((IUnknown*)link)->QueryInterface(&iid, (void**)&propertyStore).ThrowOnFailure();
        using var propertyStoreHandle = new SafeComHandle((IntPtr)propertyStore);
        SetExplicitAppUserModelId(propertyStore, explicitId);
        persistFile->Save(path, true).ThrowOnFailure();
    }

    private static unsafe void SetExplicitAppUserModelId(IPropertyStore* propertyStore, string explicitId)
    {
        PROPVARIANT value = default;
        try
        {
            value.Anonymous.Anonymous.vt = VARENUM.VT_LPWSTR;
            value.Anonymous.Anonymous.Anonymous.pwszVal = new PWSTR((char*)Marshal.StringToCoTaskMemUni(explicitId));
            propertyStore->SetValue(PInvoke.PKEY_AppUserModel_ID, value).ThrowOnFailure();
            propertyStore->Commit().ThrowOnFailure();
        }
        finally
        {
            _ = PInvoke.PropVariantClear(ref value);
        }
    }

    private static Win32AppMetadata CreateProgram(string shortcutPath)
    {
        var program = TestDataHelper.CreateTestWin32Metadata("Console", @"C:\Tools\console.exe");
        program.LnkFilePath = shortcutPath;
        return program;
    }

    private static async Task<IReadOnlyList<AppCatalogItem>> LoadItemsAsync(params Win32AppMetadata[] programs)
    {
        using var source = new Win32AppSource(
            new TestProgramSource(programs.Select(program => program.LnkFilePath).ToArray()),
            (path, asRunCommand) => programs.Single(program => program.LnkFilePath == path),
            createWatchers: false);
        return await source.LoadAsync(CancellationToken.None);
    }

    private sealed class TestProgramSource : IWin32ProgramSource
    {
        private readonly IReadOnlyList<string> _paths;

        public string Id => "test";

        public int Priority => 0;

        public bool IsEnabled => true;

        public Win32ProgramSourceProfile Profile => Win32ProgramSourceProfile.None;

        public string CacheKey => "test-key";

        public string ConfigurationKey => CacheKey;

        public IReadOnlyList<string> WatchPaths => [];

        public TestProgramSource(IReadOnlyList<string> paths)
        {
            _paths = paths;
        }

        public IEnumerable<string> GetPaths()
        {
            return _paths;
        }

        public bool IsRelevantPath(string path)
        {
            return true;
        }
    }
}
