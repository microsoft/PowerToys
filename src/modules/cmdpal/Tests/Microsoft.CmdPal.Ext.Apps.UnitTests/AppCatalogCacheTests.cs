// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppCatalogCacheTests
{
    [TestMethod]
    public async Task SaveAsync_UnchangedReconciliationDoesNotRewriteTheCacheOnEachCheck()
    {
        var cachePath = TemporaryCachePath();
        try
        {
            var created = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
            var item = Item("win32:one", TestDataHelper.CreateTestWin32Metadata("Win32"), "start-menu");
            var snapshots = new Dictionary<string, IReadOnlyList<AppCatalogItem>> { ["win32"] = [item] };
            var cache = new AppCatalogCache(cachePath);
            await cache.SaveAsync(snapshots, ["win32"], Context(created, ("win32", "key")), CancellationToken.None);
            var original = File.ReadAllBytes(cachePath);
            Assert.AreEqual(1, JsonNode.Parse(original)![nameof(AppCatalogCacheFile.SchemaVersion)]!.GetValue<int>());
            var sentinel = created.UtcDateTime;
            File.SetLastWriteTimeUtc(cachePath, sentinel);

            foreach (var minutes in new[] { 10, 20, 30, 60, 120, 359 })
            {
                await cache.SaveAsync(snapshots, ["win32"], Context(created.AddMinutes(minutes), ("win32", "key")), CancellationToken.None);
                CollectionAssert.AreEqual(original, File.ReadAllBytes(cachePath));
                Assert.AreEqual(sentinel, File.GetLastWriteTimeUtc(cachePath));
            }

            var changed = Item("win32:one", TestDataHelper.CreateTestWin32Metadata("Changed"), "start-menu");
            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>> { ["win32"] = [changed] },
                ["win32"],
                Context(created.AddHours(1), ("win32", "key")),
                CancellationToken.None);
            var loaded = await cache.LoadAsync(Context(created.AddHours(1), ("win32", "key")), CancellationToken.None);
            Assert.AreEqual("Changed", ((Win32AppPayload)loaded!.Sources.Single().Items.Single().Payload).Name);
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_IncompleteFirstScanDoesNotCreateAValidatedSource()
    {
        var cachePath = TemporaryCachePath();
        try
        {
            var cache = new AppCatalogCache(cachePath);
            var context = Context(DateTimeOffset.UtcNow, ("win32", "win32-key"));
            var items = new Dictionary<string, IReadOnlyList<AppCatalogItem>>
            {
                ["win32"] = [Item("win32:partial", TestDataHelper.CreateTestWin32Metadata("Partial"), "start-menu")],
            };
            await cache.SaveAsync(items, [], context, CancellationToken.None);
            Assert.IsNull(await cache.LoadAsync(context, CancellationToken.None));

            await cache.SaveAsync(items, ["win32"], context, CancellationToken.None);
            var validated = await cache.LoadAsync(context, CancellationToken.None);
            Assert.IsNotNull(validated);
            Assert.AreEqual(1, validated.Sources.Single().Items.Count);
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

    [TestMethod]
    public async Task SaveAsync_ChangedKeyWaitsForCompleteReconciliation()
    {
        var cachePath = TemporaryCachePath();
        try
        {
            var created = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
            var cache = new AppCatalogCache(cachePath);
            var snapshots = new Dictionary<string, IReadOnlyList<AppCatalogItem>>
            {
                ["win32"] = [Item("win32:one", TestDataHelper.CreateTestWin32Metadata("Original"), "start-menu")],
                ["packaged"] = [Item("packaged:one", TestDataHelper.CreateTestWin32Metadata("Package v1"), "packaged")],
            };
            await cache.SaveAsync(
                snapshots,
                ["win32", "packaged"],
                Context(created, ("win32", "old-key"), ("packaged", "package-key")),
                CancellationToken.None);
            var original = File.ReadAllBytes(cachePath);

            snapshots["win32"] = [Item("win32:one", TestDataHelper.CreateTestWin32Metadata("Updated"), "start-menu")];
            await cache.SaveAsync(
                snapshots,
                [],
                Context(created.AddHours(1), ("win32", "new-key"), ("packaged", "package-key")),
                CancellationToken.None);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(cachePath), "An unvalidated key change must leave the previous cache entry unchanged.");

            snapshots["packaged"] = [Item("packaged:one", TestDataHelper.CreateTestWin32Metadata("Package v2"), "packaged")];
            var changedContext = Context(created.AddHours(1), ("win32", "new-key"), ("packaged", "new-package-key"));
            await cache.SaveAsync(snapshots, ["packaged"], changedContext, CancellationToken.None);
            var current = await new AppCatalogCache(cachePath).LoadAsync(changedContext, CancellationToken.None);
            Assert.IsNotNull(current);
            Assert.AreEqual("packaged", current.Sources.Single().SourceId);
            Assert.AreEqual("Package v2", ((Win32AppPayload)current.Sources.Single().Items.Single().Payload).Name);

            var previous = await new AppCatalogCache(cachePath).LoadAsync(
                Context(created.AddHours(1), ("win32", "old-key"), ("packaged", "new-package-key")),
                CancellationToken.None);
            Assert.IsNotNull(previous);
            var retained = previous.Sources.Single(source => source.SourceId == "win32");
            Assert.AreEqual(created, retained.ValidatedAtUtc);
            Assert.AreEqual("Original", ((Win32AppPayload)retained.Items.Single().Payload).Name);

            var reconciledContext = Context(created.AddHours(2), ("win32", "new-key"), ("packaged", "new-package-key"));
            await cache.SaveAsync(snapshots, ["win32"], reconciledContext, CancellationToken.None);
            var reconciled = await new AppCatalogCache(cachePath).LoadAsync(reconciledContext, CancellationToken.None);
            Assert.IsNotNull(reconciled);
            Assert.AreEqual(2, reconciled.Sources.Count);
            var updated = reconciled.Sources.Single(source => source.SourceId == "win32");
            Assert.AreEqual(created.AddHours(2), updated.ValidatedAtUtc);
            Assert.AreEqual("Updated", ((Win32AppPayload)updated.Items.Single().Payload).Name);
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

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
            var program = TestDataHelper.CreateTestWin32Metadata("Cached app");
            program.IconLocation = @"C:\Icons, custom\icons.dll,-4";
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
            Assert.AreEqual(program.IconLocation, app.IconSource);
            Assert.AreEqual(program.TargetPath, app.LaunchTarget);
            Assert.AreEqual(program.AppExecutionAlias.TargetPath, app.ResolvedTarget);
            Assert.AreEqual(program.Arguments, app.LaunchArguments);
            Assert.AreEqual(program.AppExecutionAlias.Aumid, app.AppUserModelId);
            Assert.IsFalse(app.IsPackaged);
            Assert.AreEqual(new AppCommand(item.ToAppItem()).Id, new AppCommand(app).Id);
            CollectionAssert.AreEqual(item.ToAppItem().CommandIds.ToArray(), app.CommandIds.ToArray());
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
    public async Task SaveAndLoadAsync_ShortcutDisplayNamePreservesRawNameAndCommandIds()
    {
        var cachePath = TemporaryCachePath();
        try
        {
            var context = Context(DateTimeOffset.UtcNow, ("win32", "win32-key"));
            var program = TestDataHelper.CreateTestWin32Metadata("Raw shortcut");
            program.DisplayName = "Localized shortcut";
            program.LnkFilePath = @"C:\Links\Raw shortcut.lnk";
            program.AppType = Programs.Win32AppType.ShortcutApplication;
            var item = Item("win32:shortcut", program, "start-menu");
            var releasedId = AppCommand.GenerateId(program.Name, program.Description, program.LnkFilePath);
            var canonicalId = new AppCommand(item.ToAppItem()).Id;
            var cache = new AppCatalogCache(cachePath);

            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>> { ["win32"] = [item] },
                ["win32"],
                context,
                CancellationToken.None);
            var loaded = await cache.LoadAsync(context, CancellationToken.None);

            Assert.IsNotNull(loaded);
            var cachedItem = loaded.Sources.Single().Items.Single();
            var payload = (Win32AppPayload)cachedItem.Payload;
            Assert.AreEqual(program.Name, payload.Name);
            Assert.AreEqual(program.Name, Path.GetFileNameWithoutExtension(payload.LnkFilePath));
            Assert.AreEqual(program.DisplayName, payload.DisplayName);
            Assert.AreEqual(program.TargetPath, payload.TargetPath);
            Assert.AreEqual(program.LnkFilePath, payload.LnkFilePath);
            Assert.AreEqual(releasedId, payload.GetCommandId());
            Assert.AreEqual(item.Identity, cachedItem.Identity);
            Assert.IsTrue(item.HasSamePersistedContent(cachedItem));
            CollectionAssert.AreEqual(item.CommandIds.ToArray(), cachedItem.CommandIds.ToArray());

            var app = cachedItem.ToAppItem();
            Assert.AreEqual(program.DisplayName, app.Name);
            Assert.AreEqual(program.LnkFilePath, app.LaunchTarget);
            Assert.AreEqual(canonicalId, new AppCommand(app).Id);
            var row = new Programs.AppListItem(app, useThumbnails: false);
            var snapshot = new AppListItemSnapshot([row], []);
            Assert.AreSame(row, snapshot.GetVisibleApp(canonicalId));
            Assert.AreSame(row, snapshot.GetVisibleApp(releasedId));
            Assert.AreEqual(releasedId, snapshot.GetCommandItem(releasedId)?.Command?.Id);
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoadAsync_ShortcutPayloadWithMissingOrNullDisplayNameFallsBackToRawName(bool explicitNull)
    {
        var cachePath = TemporaryCachePath();
        try
        {
            var context = Context(DateTimeOffset.UtcNow, ("win32", "win32-key"));
            var program = TestDataHelper.CreateTestWin32Metadata("Raw shortcut");
            program.LnkFilePath = @"C:\Links\Raw shortcut.lnk";
            program.AppType = Programs.Win32AppType.ShortcutApplication;
            var item = Item("win32:shortcut", program, "start-menu");
            var releasedId = item.Payload.GetCommandId();
            var canonicalId = new AppCommand(item.ToAppItem()).Id;
            var cache = new AppCatalogCache(cachePath);
            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>> { ["win32"] = [item] },
                ["win32"],
                context,
                CancellationToken.None);
            var content = JsonNode.Parse(File.ReadAllText(cachePath))!;
            var payloadJson = content["Sources"]![0]!["Items"]![0]!["Payload"]!.AsObject();
            if (explicitNull)
            {
                payloadJson["DisplayName"] = null;
            }
            else
            {
                Assert.IsTrue(payloadJson.Remove("DisplayName"));
            }

            File.WriteAllText(cachePath, content.ToJsonString());
            var loaded = await cache.LoadAsync(context, CancellationToken.None);

            Assert.IsNotNull(loaded);
            var cachedItem = loaded.Sources.Single().Items.Single();
            var payload = (Win32AppPayload)cachedItem.Payload;
            Assert.AreEqual(string.Empty, payload.DisplayName);
            Assert.AreEqual(program.Name, payload.Name);
            Assert.AreEqual(releasedId, payload.GetCommandId());
            Assert.IsTrue(item.HasSamePersistedContent(cachedItem));
            var app = cachedItem.ToAppItem();
            Assert.AreEqual(program.Name, app.Name);
            Assert.AreEqual(canonicalId, new AppCommand(app).Id);
            var row = new Programs.AppListItem(app, useThumbnails: false);
            var snapshot = new AppListItemSnapshot([row], []);
            Assert.AreSame(row, snapshot.GetVisibleApp(releasedId));
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
                ["edit.exe"],
                new PackagedAppSnapshot
                {
                    Name = "Cached package",
                    PackageFullName = "Cached.Package_1.0.0.0_x64__publisher",
                    Executable = @"Tools\Editor.exe",
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
            Assert.AreEqual(@"Tools\Editor.exe", ((PackagedAppSnapshot)payload).Executable);
            CollectionAssert.Contains(loaded.Sources[0].Items[0].MatchTerms.ToArray(), "edit.exe");
            Assert.IsTrue(item.HasSamePersistedContent(loaded.Sources[0].Items[0]));
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoadAsync_PackagedPayloadWithMissingOrNullExecutableRemainsCompatible(bool explicitNull)
    {
        var cachePath = TemporaryCachePath();
        try
        {
            var context = Context(DateTimeOffset.UtcNow, ("packaged", "package-key"));
            var item = new AppCatalogItem(
                "packaged:cached",
                0,
                new AppCatalogSourceReference("packaged", "Cached.Package!App"),
                [],
                new PackagedAppSnapshot { Name = "Cached package" });
            var cache = new AppCatalogCache(cachePath);
            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>> { ["packaged"] = [item] },
                ["packaged"],
                context,
                CancellationToken.None);
            var content = JsonNode.Parse(File.ReadAllText(cachePath))!;
            var payload = content["Sources"]![0]!["Items"]![0]!["Payload"]!.AsObject();
            if (explicitNull)
            {
                payload["Executable"] = null;
            }
            else
            {
                Assert.IsTrue(payload.Remove("Executable"));
            }

            File.WriteAllText(cachePath, content.ToJsonString());

            var loaded = await cache.LoadAsync(context, CancellationToken.None);

            Assert.IsNotNull(loaded);
            var cachedPayload = (PackagedAppSnapshot)loaded.Sources.Single().Items.Single().Payload;
            Assert.AreEqual(string.Empty, cachedPayload.Executable);
            Assert.AreEqual(item.Payload.GetCommandId(), cachedPayload.GetCommandId());
            Assert.IsTrue(item.HasSamePersistedContent(loaded.Sources.Single().Items.Single()));
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
                    ["packaged"] = [Item("packaged:one", TestDataHelper.CreateTestWin32Metadata("Packaged"), "packaged")],
                    ["win32"] = [Item("win32:one", TestDataHelper.CreateTestWin32Metadata("Win32"), "start-menu")],
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
            var unchanged = Item("win32:one", TestDataHelper.CreateTestWin32Metadata("Win32"), "start-menu");
            var packaged = Item("packaged:one", TestDataHelper.CreateTestWin32Metadata("Packaged v1"), "packaged");
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
                TestDataHelper.CreateTestWin32Metadata("Packaged v2"),
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
    public async Task SaveAsync_FullReconciliationRenewsUnchangedSourceValidationAfterWriteInterval()
    {
        var cachePath = TemporaryCachePath();
        try
        {
            var firstValidation = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
            var sourceKeys = new[] { ("win32", "win32-key") };
            var item = Item("win32:one", TestDataHelper.CreateTestWin32Metadata("Win32"), "start-menu");
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
                Context(firstValidation.AddHours(6), sourceKeys),
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
    public async Task SaveAsync_SameKeyIncrementalChangeDoesNotRenewSourceValidation()
    {
        var cachePath = TemporaryCachePath();
        try
        {
            var firstValidation = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
            var original = Item("win32:one", TestDataHelper.CreateTestWin32Metadata("Original"), "start-menu");
            var updated = Item("win32:one", TestDataHelper.CreateTestWin32Metadata("Updated"), "start-menu");
            var cache = new AppCatalogCache(cachePath);

            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>> { ["win32"] = [original] },
                ["win32"],
                Context(firstValidation, ("win32", "key")),
                CancellationToken.None);
            await cache.SaveAsync(
                new Dictionary<string, IReadOnlyList<AppCatalogItem>> { ["win32"] = [updated] },
                [],
                Context(firstValidation.AddHours(1), ("win32", "key")),
                CancellationToken.None);

            var current = await cache.LoadAsync(
                Context(firstValidation.AddHours(35), ("win32", "key")),
                CancellationToken.None);
            var expired = await cache.LoadAsync(
                Context(firstValidation.AddHours(36).AddMinutes(30), ("win32", "key")),
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
        Programs.Win32AppMetadata program,
        string sourceId)
    {
        return new(
            identity,
            priority: 0,
            new AppCatalogSourceReference(sourceId, program.TargetPath),
            [],
            Win32AppPayload.From(program));
    }

    private static string TemporaryCachePath()
    {
        return Path.Combine(Path.GetTempPath(), $"cmdpal-app-catalog-{Guid.NewGuid():N}.json");
    }
}
