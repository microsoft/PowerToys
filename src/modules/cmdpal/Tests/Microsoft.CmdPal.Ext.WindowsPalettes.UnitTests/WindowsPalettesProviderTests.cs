// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;
using Windows.UI.Palettes;

namespace Microsoft.CmdPal.Ext.WindowsPalettes.UnitTests;

[TestClass]
public sealed partial class WindowsPalettesProviderTests
{
    [TestMethod]
    public void ApiPresenceCheckUsesPalettesContractVersionTwo()
    {
        var actualContractName = string.Empty;
        ushort actualMajorVersion = 0;
        ushort actualMinorVersion = 0;

        using var provider = WindowsPalettesCommandsProvider.TryCreate(
            (contractName, majorVersion, minorVersion) =>
            {
                actualContractName = contractName;
                actualMajorVersion = majorVersion;
                actualMinorVersion = minorVersion;
                return false;
            });

        Assert.IsNull(provider);
        Assert.AreEqual("Windows.UI.Palettes.PalettesContract", actualContractName);
        Assert.AreEqual((ushort)2, actualMajorVersion);
        Assert.AreEqual((ushort)0, actualMinorVersion);
    }

    [TestMethod]
    public void CatalogInitializationDoesNotBlockProviderCreation()
    {
        using ManualResetEventSlim initializationStarted = new();
        using ManualResetEventSlim continueInitialization = new();
        WindowsPalettesCommandsProvider provider = null!;

        try
        {
            var stopwatch = Stopwatch.StartNew();
            provider = WindowsPalettesCommandsProvider.TryCreate(
                static (_, _, _) => true,
                () =>
                {
                    initializationStarted.Set();
                    continueInitialization.Wait();
                });
            stopwatch.Stop();

            Assert.IsNotNull(provider);
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
            Assert.IsTrue(initializationStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(0, provider.TopLevelCommands().Length);
        }
        finally
        {
            provider?.Dispose();
            continueInitialization.Set();
        }
    }

    [TestMethod]
    [DataRow("extension", "item", "Invoke")]
    [DataRow("extension:with:colons", "item/with/unicode-\u2603", "Children")]
    public void CommandIdRoundTrips(string extensionId, string itemId, string kindName)
    {
        var kind = Enum.Parse<WupCommandKind>(kindName);
        var id = WupCommandId.Create(extensionId, itemId, kind);

        Assert.IsTrue(WupCommandId.TryParse(id, out var identity));
        Assert.AreEqual(extensionId, identity.ExtensionId);
        Assert.AreEqual(itemId, identity.ItemId);
        Assert.AreEqual(kind, identity.Kind);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not-a-wup-command")]
    [DataRow("wup-item:v1:x:ZXh0ZW5zaW9u:aXRlbQ")]
    [DataRow("wup-item:v1:i:not+base64:aXRlbQ")]
    public void CommandIdRejectsInvalidValues(string id)
    {
        Assert.IsFalse(WupCommandId.TryParse(id, out _));
    }

    [TestMethod]
    public void MissingApiErrorsAreRecognized()
    {
        Assert.IsTrue(WindowsPalettesCommandsProvider.IsApiMissing(new TypeLoadException()));
        Assert.IsTrue(WindowsPalettesCommandsProvider.IsApiMissing(new EntryPointNotFoundException()));
        Assert.IsTrue(WindowsPalettesCommandsProvider.IsApiMissing(Marshal.GetExceptionForHR(unchecked((int)0x80040154))!));
        Assert.IsTrue(WindowsPalettesCommandsProvider.IsApiMissing(Marshal.GetExceptionForHR(unchecked((int)0x80040111))!));
        Assert.IsTrue(WindowsPalettesCommandsProvider.IsApiMissing(Marshal.GetExceptionForHR(unchecked((int)0x8007007E))!));
    }

    [TestMethod]
    public void OtherInitializationErrorsAreNotMissingApiErrors()
    {
        Assert.IsFalse(WindowsPalettesCommandsProvider.IsApiMissing(new InvalidOperationException()));
    }

    [TestMethod]
    public void CatalogRefreshRetainsPagesAndUpdatesDefinitionsInCatalogOrder()
    {
        using var page = new WindowsPalettesPage(Array.Empty<PaletteExtensionDefinition>());
        var first = new TestDefinition("first");
        var second = new TestDefinition("second");
        page.UpdateSources(new IPaletteExtensionDefinition[] { first, second });
        var originalSources = page.GetSourcesSnapshot();

        var updatedFirst = new TestDefinition("first") { DisplayName = "Updated name" };
        page.UpdateSources(new IPaletteExtensionDefinition[] { second, updatedFirst });
        var sources = page.GetSourcesSnapshot();

        Assert.HasCount(2, sources);
        Assert.AreSame(originalSources[1], sources[0]);
        Assert.AreSame(originalSources[0], sources[1]);
        Assert.AreSame(updatedFirst, sources[1].Definition);
        Assert.AreEqual("Updated name", sources[1].Name);
        Assert.AreEqual("Updated name", sources[1].Title);
        Assert.AreEqual("Updated name", sources[1].PlaceholderText);
    }

    [TestMethod]
    public void CatalogRefreshAddsPagesAndDisposesOnlyRemovedSources()
    {
        using var page = new WindowsPalettesPage(Array.Empty<PaletteExtensionDefinition>());
        var removed = new TestDefinition("removed");
        var retained = new TestDefinition("retained");
        page.UpdateSources(new IPaletteExtensionDefinition[] { removed, retained });
        var originalSources = page.GetSourcesSnapshot();
        RehydrateMissingItem(originalSources[0], removed);
        RehydrateMissingItem(originalSources[1], retained);
        Assert.AreEqual(1, removed.Source.SubscriptionCount);
        Assert.AreEqual(1, retained.Source.SubscriptionCount);

        var added = new TestDefinition("added");
        page.UpdateSources(new IPaletteExtensionDefinition[] { retained, added });
        var sources = page.GetSourcesSnapshot();

        Assert.HasCount(2, sources);
        Assert.AreSame(originalSources[1], sources[0]);
        Assert.AreSame(added, sources[1].Definition);
        Assert.AreEqual(0, removed.Source.SubscriptionCount);
        Assert.AreEqual(1, retained.Source.SubscriptionCount);
        RehydrateMissingItem(originalSources[0], removed);
        Assert.AreEqual(1, removed.CreationCount);
    }

    [TestMethod]
    public void RefreshedDefinitionRetainsTheActiveExtension()
    {
        using var page = new WindowsPalettesPage(Array.Empty<PaletteExtensionDefinition>());
        var original = new TestDefinition("source");
        page.UpdateSources(new IPaletteExtensionDefinition[] { original });
        var source = page.GetSourcesSnapshot()[0];
        RehydrateMissingItem(source, original);
        Assert.AreEqual(1, original.CreationCount);

        var updated = new TestDefinition("source");
        page.UpdateSources(new IPaletteExtensionDefinition[] { updated });
        RehydrateMissingItem(page.GetSourcesSnapshot()[0], updated);

        Assert.AreSame(source, page.GetSourcesSnapshot()[0]);
        Assert.AreEqual(1, original.CreationCount);
        Assert.AreEqual(0, updated.CreationCount);
        Assert.AreEqual(1, original.Source.SubscriptionCount);
    }

    [TestMethod]
    public void ActivationAfterUnloadUsesTheLatestDefinition()
    {
        using var page = new WindowsPalettesPage(Array.Empty<PaletteExtensionDefinition>());
        var original = new TestDefinition("source");
        page.UpdateSources(new IPaletteExtensionDefinition[] { original });
        var source = page.GetSourcesSnapshot()[0];
        RehydrateMissingItem(source, original);
        Assert.IsNotNull(original.Host);

        var updated = new TestDefinition("source");
        page.UpdateSources(new IPaletteExtensionDefinition[] { updated });
        original.Host.RequestUnload();
        Assert.IsTrue(SpinWait.SpinUntil(() => original.Source.SubscriptionCount == 0, TimeSpan.FromSeconds(5)));
        RehydrateMissingItem(source, updated);

        Assert.AreEqual(1, original.CreationCount);
        Assert.AreEqual(1, updated.CreationCount);
        Assert.AreEqual(1, updated.Source.SubscriptionCount);
    }

    [TestMethod]
    [DataRow("other-package", "app", "source")]
    [DataRow("package", "other-app", "source")]
    [DataRow("package", "app", "other-source")]
    public void ProviderIdentityIncludesPackageApplicationAndExtension(string package, string application, string extension)
    {
        using var page = new WindowsPalettesPage(Array.Empty<PaletteExtensionDefinition>());
        var original = new TestDefinition("source");
        page.UpdateSources(new IPaletteExtensionDefinition[] { original });
        var originalSource = page.GetSourcesSnapshot()[0];
        RehydrateMissingItem(originalSource, original);

        var replacement = new TestDefinition(extension) { PackageFamilyName = package, ApplicationId = application };
        page.UpdateSources(new IPaletteExtensionDefinition[] { replacement });

        Assert.AreNotSame(originalSource, page.GetSourcesSnapshot()[0]);
        Assert.AreEqual(0, original.Source.SubscriptionCount);
    }

    [TestMethod]
    public void EmptyCatalogDisposesAllSourcesAndCanAddThemAgain()
    {
        using var page = new WindowsPalettesPage(Array.Empty<PaletteExtensionDefinition>());
        var definition = new TestDefinition("source");
        page.UpdateSources(new IPaletteExtensionDefinition[] { definition });
        var originalSource = page.GetSourcesSnapshot()[0];
        RehydrateMissingItem(originalSource, definition);

        page.UpdateSources(Array.Empty<IPaletteExtensionDefinition>());

        Assert.HasCount(0, page.GetSourcesSnapshot());
        Assert.AreEqual(0, definition.Source.SubscriptionCount);

        page.UpdateSources(new IPaletteExtensionDefinition[] { definition });
        Assert.AreNotSame(originalSource, page.GetSourcesSnapshot()[0]);
    }

    [TestMethod]
    public void DisposedPageIgnoresCatalogRefresh()
    {
        var page = new WindowsPalettesPage(Array.Empty<PaletteExtensionDefinition>());
        page.Dispose();

        page.UpdateSources(new IPaletteExtensionDefinition[] { new TestDefinition("source") });

        Assert.HasCount(0, page.GetSourcesSnapshot());
    }

    private static void RehydrateMissingItem(WupSourcePage source, TestDefinition definition)
    {
        var identity = new WupCommandIdentity(definition.ExtensionId, "missing", WupCommandKind.Invoke);
        var commandId = WupCommandId.Create(identity.ExtensionId, identity.ItemId, identity.Kind);
        Assert.IsNull(source.TryRehydrateCommandItem(identity, commandId));
    }

    private sealed partial class TestDefinition(string extensionId) : IPaletteExtensionDefinition
    {
        public string ExtensionId { get; } = extensionId;

        public string PackageFamilyName { get; init; } = "package";

        public string ApplicationId { get; init; } = "app";

        public string DisplayName { get; init; } = extensionId;

        public PaletteIconData Icon => null!;

        public TestSource Source { get; } = new();

        public int CreationCount { get; private set; }

        public PaletteExtensionHost Host { get; private set; } = null!;

        public PaletteItemFactory ItemFactory => throw new NotSupportedException();

        public string Description => string.Empty;

        public string[] AcceptedKindSpecs => [];

        public string Publisher => string.Empty;

        public IDictionary<string, object> Properties => throw new NotSupportedException();

        public PaletteKinds Kinds => throw new NotSupportedException();

        public PaletteKinds ContainedKinds => throw new NotSupportedException();

        public IPaletteItemSourceExtension CreateExtension(PaletteExtensionHost host)
        {
            CreationCount++;
            Host = host;
            return Source;
        }
    }

    private sealed partial class TestSource : IPaletteItemSourceExtension
    {
        private int _subscriptionCount;

        public int SubscriptionCount => Volatile.Read(ref _subscriptionCount);

        public event TypedEventHandler<IPaletteItemSourceExtension, object> ItemsChanged
        {
            add => Interlocked.Increment(ref _subscriptionCount);
            remove => Interlocked.Decrement(ref _subscriptionCount);
        }

        public void Initialize(PaletteItemFactory factory, PaletteExtensionHost host)
        {
        }

        public PaletteItemView GetItem(string id) => null!;

        public PaletteItemView[] GetItems() => [];

        public IAsyncOperationWithProgress<PaletteQueryResultsView, PaletteQueryProgress> ExecuteQueryAsync(PaletteQuery query) =>
            throw new NotSupportedException();

        public IAsyncAction ReportItemAnalyticsAsync(PaletteItemAnalytics[] analytics) => throw new NotSupportedException();

        public IAsyncAction ReportConversationAnalyticsAsync(PaletteItemAnalytics[] itemAnalytics) => throw new NotSupportedException();
    }
}
