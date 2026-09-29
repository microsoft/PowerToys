// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppCatalogCacheTests
{
    [DataTestMethod]
    [DataRow("null")]
    [DataRow("[null]")]
    [DataRow("[{\"SourceId\":\"win32\",\"SourceKey\":\"win32-key\",\"ValidatedAtUtc\":\"2026-08-01T12:00:00+00:00\",\"Items\":null}]")]
    [DataRow("[{\"SourceId\":\"win32\",\"SourceKey\":\"win32-key\",\"ValidatedAtUtc\":\"2026-08-01T12:00:00+00:00\",\"Items\":[null]}]")]
    public async Task LoadAsync_NullCacheEntriesAreIgnored(string sourcesJson)
    {
        var cachePath = TemporaryCachePath();
        try
        {
            File.WriteAllText(cachePath, $$"""{"SchemaVersion":{{AppCatalogCacheFile.CurrentSchemaVersion}},"Language":"en-US","Sources":{{sourcesJson}}}""");
            var cache = new AppCatalogCache(cachePath);
            var context = Context(new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero), ("win32", "win32-key"));

            Assert.IsNull(await cache.LoadAsync(context, CancellationToken.None));

            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>> { ["win32"] = [] },
                ["win32"],
                context,
                CancellationToken.None);
            Assert.IsNotNull(await cache.LoadAsync(context, CancellationToken.None));
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_MatchingSource_RoundTripsCatalogAndProvenance()
    {
        var cachePath = TemporaryCachePath();
        try
        {
            var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
            var context = Context(now, ("win32", "win32-key"));
            var program = TestDataHelper.CreateTestWin32Program("Cached app");
            program.IcoPath = @"C:\Icons, custom\icons.dll,-4";
            program.Arguments = "--profile cached";
            program.WorkingDirectory = @"C:\Projects\Cached";
            program.AppExecutionAlias = new Programs.ReparsePoint.AppExecutionAliasInfo
            {
                Aumid = "Contoso.Cached_123!app",
                TargetPath = @"C:\Program Files\WindowsApps\Contoso.Cached_1.0.0.0_x64__123\app.exe",
            };
            var item = Item("win32:cached", program, "start-menu");
            var legacyPayload = Win32AppPayload.From(program) with { Name = "Legacy name", LnkFilePath = @"C:\Links\Legacy.lnk" };
            var legacyId = new AppCommand(legacyPayload.ToAppItem()).Id;
            item = item.MergeProvenance(new AppCatalogItem(item.Identity, 10, new AppCatalogSourceReference("start-menu", legacyPayload.LnkFilePath), [], legacyPayload));
            item = item.WithIdentity("win32:canonical-cache-identity");
            var cache = new AppCatalogCache(cachePath);

            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>> { ["win32"] = [item] },
                ["win32"],
                context,
                CancellationToken.None);
            var loaded = await cache.LoadAsync(context, CancellationToken.None);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.Sources.Count);
            Assert.AreEqual("Cached app", loaded.Sources[0].Items[0].ToAppItem().Name);
            Assert.IsTrue(item.HasSamePersistedContent(loaded.Sources[0].Items[0]));
            CollectionAssert.AreEqual(item.IdentityAliases.ToArray(), loaded.Sources[0].Items[0].IdentityAliases.ToArray());
            CollectionAssert.Contains(loaded.Sources[0].Items[0].IdentityAliases.ToArray(), "win32:cached");
            var payload = loaded.Sources[0].Items[0].Payload as Win32AppPayload;
            Assert.IsNotNull(payload);
            Assert.AreEqual("--profile cached", payload.Arguments);
            Assert.AreEqual(program.WorkingDirectory, payload.WorkingDirectory);
            Assert.AreEqual("Contoso.Cached_123!app", payload.PackagedAppUserModelId);
            Assert.AreEqual(program.AppExecutionAlias.TargetPath, payload.AppExecutionAliasTargetPath);
            var app = loaded.Sources[0].Items[0].ToAppItem();
            Assert.AreEqual(program.IcoPath, app.IcoPath);
            Assert.AreEqual(program.FullPath, app.ExePath);
            Assert.AreEqual(program.AppExecutionAlias.TargetPath, app.FullExecutablePath);
            Assert.AreEqual(program.Arguments, app.Arguments);
            Assert.AreEqual(program.AppExecutionAlias.Aumid, app.UserModelId);
            Assert.IsFalse(app.IsPackaged);
            Assert.AreEqual(new AppCommand(item.ToAppItem()).Id, new AppCommand(app).Id);
            CollectionAssert.AreEqual(item.CommandIds.ToArray(), app.CommandIds.ToArray());
            var row = new Programs.AppListItem(app, useThumbnails: false);
            var snapshot = new AppListItemSnapshot([row], []);
            Assert.AreSame(row, snapshot.GetVisibleApp(legacyId));
            Assert.AreEqual(legacyId, snapshot.GetCommandItem(legacyId)?.Command?.Id);
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_PackagedPayload_RoundTripsPolymorphically()
    {
        var cachePath = TemporaryCachePath();
        try
        {
            var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
            var context = Context(now, ("packaged", "package-key"));
            var item = new AppCatalogItem(
                "packaged:cached",
                priority: 0,
                new AppCatalogSourceReference("packaged", "Cached.Package!App"),
                [],
                new PackagedAppSnapshot
                {
                    Name = "Cached package",
                    PackageFullName = "Cached.Package_1.0.0.0_x64__publisher",
                });
            var cache = new AppCatalogCache(cachePath);

            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>> { ["packaged"] = [item] },
                ["packaged"],
                context,
                CancellationToken.None);
            var loaded = await cache.LoadAsync(context, CancellationToken.None);

            Assert.IsNotNull(loaded);
            var payload = loaded.Sources[0].Items[0].Payload;
            Assert.IsInstanceOfType<PackagedAppSnapshot>(payload);
            Assert.AreEqual("Cached package", payload.ToAppItem().Name);
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

    [TestMethod]
    public async Task LoadAsync_IncompatibleLegacyItemShape_ReturnsNull()
    {
        var cachePath = TemporaryCachePath();
        try
        {
            var legacyCache = $$"""
                {
                  "SchemaVersion": {{AppCatalogCacheFile.CurrentSchemaVersion - 1}},
                  "Language": "en-US",
                  "Sources": [
                    {
                      "SourceId": "win32",
                      "SourceKey": "win32-key",
                      "ValidatedAtUtc": "2026-08-01T12:00:00+00:00",
                      "Items": [
                        {
                          "Identity": "win32:legacy",
                          "Priority": 0,
                          "SourceReferences": []
                        }
                      ]
                    }
                  ]
                }
                """;
            File.WriteAllText(cachePath, legacyCache);
            var cache = new AppCatalogCache(cachePath);

            var loaded = await cache.LoadAsync(
                Context(new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero), ("win32", "win32-key")),
                CancellationToken.None);

            Assert.IsNull(loaded);
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

    [TestMethod]
    public async Task LoadAsync_SourceKeyChange_InvalidatesOnlyAffectedSource()
    {
        var cachePath = TemporaryCachePath();
        try
        {
            var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
            var cache = new AppCatalogCache(cachePath);
            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>>
                {
                    ["packaged"] = [Item("packaged:one", TestDataHelper.CreateTestWin32Program("Packaged"), "packaged")],
                    ["win32"] = [Item("win32:one", TestDataHelper.CreateTestWin32Program("Win32"), "start-menu")],
                },
                ["packaged", "win32"],
                Context(now, ("packaged", "package-key"), ("win32", "win32-key")),
                CancellationToken.None);

            var loaded = await cache.LoadAsync(
                Context(now, ("packaged", "changed-package-key"), ("win32", "win32-key")),
                CancellationToken.None);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.Sources.Count);
            Assert.AreEqual("win32", loaded.Sources[0].SourceId);
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_UnchangedSourceDoesNotInheritAnotherSourcesNewAge()
    {
        var cachePath = TemporaryCachePath();
        try
        {
            var created = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
            var cache = new AppCatalogCache(cachePath);
            var unchanged = Item("win32:one", TestDataHelper.CreateTestWin32Program("Win32"), "start-menu");
            var packaged = Item("packaged:one", TestDataHelper.CreateTestWin32Program("Packaged v1"), "packaged");
            var sourceKeys = new[] { ("packaged", "package-key"), ("win32", "win32-key") };

            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>>
                {
                    ["packaged"] = [packaged],
                    ["win32"] = [unchanged],
                },
                ["packaged", "win32"],
                Context(created, sourceKeys),
                CancellationToken.None);

            var updatedPackaged = Item(
                "packaged:one",
                TestDataHelper.CreateTestWin32Program("Packaged v2"),
                "packaged");
            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>>
                {
                    ["packaged"] = [updatedPackaged],
                    ["win32"] = [unchanged],
                },
                ["packaged"],
                Context(created.AddHours(1), sourceKeys),
                CancellationToken.None);

            var loaded = await cache.LoadAsync(
                Context(created.AddHours(36).AddMinutes(30), sourceKeys),
                CancellationToken.None);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.Sources.Count);
            Assert.AreEqual("packaged", loaded.Sources[0].SourceId);
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_FullReconciliationRenewsUnchangedSourceValidation()
    {
        var cachePath = TemporaryCachePath();
        try
        {
            var firstValidation = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
            var sourceKeys = new[] { ("win32", "win32-key") };
            var item = Item("win32:one", TestDataHelper.CreateTestWin32Program("Win32"), "start-menu");
            var snapshots = new Dictionary<string, IReadOnlyList<AppCatalogItem>> { ["win32"] = [item] };
            var cache = new AppCatalogCache(cachePath);

            await cache.SaveAsync(
                snapshots,
                ["win32"],
                Context(firstValidation, sourceKeys),
                CancellationToken.None);
            await cache.SaveAsync(
                snapshots,
                ["win32"],
                Context(firstValidation.AddHours(1), sourceKeys),
                CancellationToken.None);

            var loaded = await cache.LoadAsync(
                Context(firstValidation.AddHours(36).AddMinutes(30), sourceKeys),
                CancellationToken.None);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.Sources.Count);
            Assert.AreEqual("win32", loaded.Sources[0].SourceId);
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_IncrementalChangeDoesNotRenewSourceValidation()
    {
        var cachePath = TemporaryCachePath();
        try
        {
            var firstValidation = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
            var original = Item("win32:one", TestDataHelper.CreateTestWin32Program("Original"), "start-menu");
            var updated = Item("win32:one", TestDataHelper.CreateTestWin32Program("Updated"), "start-menu");
            var cache = new AppCatalogCache(cachePath);

            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>> { ["win32"] = [original] },
                ["win32"],
                Context(firstValidation, ("win32", "key-before-change")),
                CancellationToken.None);
            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>> { ["win32"] = [updated] },
                [],
                Context(firstValidation.AddHours(1), ("win32", "key-after-change")),
                CancellationToken.None);

            var current = await cache.LoadAsync(
                Context(firstValidation.AddHours(35), ("win32", "key-after-change")),
                CancellationToken.None);
            var expired = await cache.LoadAsync(
                Context(firstValidation.AddHours(36).AddMinutes(30), ("win32", "key-after-change")),
                CancellationToken.None);

            Assert.IsNotNull(current);
            Assert.AreEqual("Updated", (current.Sources[0].Items[0].Payload as Win32AppPayload)?.Name);
            Assert.IsNull(expired);
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

    [TestMethod]
    public void IsCompatible_RejectsLanguageAndSchemaChanges()
    {
        var cache = new AppCatalogCacheFile { Language = "en-US" };

        Assert.IsTrue(cache.IsCompatible(Context(DateTimeOffset.UtcNow, ("win32", "key"))));
        Assert.IsFalse(cache.IsCompatible(new AppCatalogCacheContext
        {
            Language = "de-DE",
            SourceKeys = new Dictionary<string, string>(),
            NowUtc = DateTimeOffset.UtcNow,
        }));

        cache.SchemaVersion = AppCatalogCacheFile.CurrentSchemaVersion - 1;
        Assert.IsFalse(cache.IsCompatible(Context(DateTimeOffset.UtcNow, ("win32", "key"))));
    }

    private static AppCatalogCacheContext Context(
        DateTimeOffset now,
        params (string SourceId, string SourceKey)[] sources)
    {
        var sourceKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            sourceKeys[source.SourceId] = source.SourceKey;
        }

        return new AppCatalogCacheContext
        {
            Language = "en-US",
            SourceKeys = sourceKeys,
            NowUtc = now,
        };
    }

    private static AppCatalogItem Item(
        string identity,
        Programs.Win32Program program,
        string sourceId)
        => new(
            identity,
            priority: 0,
            new AppCatalogSourceReference(sourceId, program.FullPath),
            [],
            Win32AppPayload.From(program));

    private static string TemporaryCachePath()
        => Path.Combine(Path.GetTempPath(), $"cmdpal-app-catalog-{Guid.NewGuid():N}.json");
}
