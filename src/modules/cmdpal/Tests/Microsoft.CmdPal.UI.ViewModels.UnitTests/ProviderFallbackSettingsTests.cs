// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class ProviderFallbackSettingsTests
{
    private const string ProviderId = "test-provider";
    private const string FallbackId = "test-provider.fallback";

    [TestMethod]
    public async Task LoadTopLevelCommands_BuiltInFallback_IsPersistedInGlobalResults()
    {
        var settingsService = new FakeSettingsService();
        await using var services = CreateServices(settingsService);
        var wrapper = new CommandProviderWrapper(new TestCommandProvider(), TaskScheduler.Default);

        await wrapper.LoadTopLevelCommands(services);

        var fallbackSettings = settingsService.Settings.ProviderSettings[ProviderId].FallbackCommands[FallbackId];
        Assert.IsTrue(fallbackSettings.IsEnabled);
        Assert.IsTrue(fallbackSettings.IncludeInGlobalResults);
        Assert.IsTrue(wrapper.FallbackItems[0].IncludeInGlobalResults);
        CollectionAssert.Contains(settingsService.Settings.GetGlobalFallbacks(), FallbackId);
    }

    [TestMethod]
    public async Task LoadTopLevelCommands_KeepsSavedFallbackSettings()
    {
        var settingsService = new FakeSettingsService();
        var saved = new ProviderSettings
        {
            FallbackCommands = ImmutableDictionary<string, FallbackSettings>.Empty.Add(FallbackId, new FallbackSettings { IsEnabled = true, IncludeInGlobalResults = false }),
        };
        settingsService.Settings = settingsService.Settings with
        {
            ProviderSettings = settingsService.Settings.ProviderSettings.SetItem(ProviderId, saved),
        };
        await using var services = CreateServices(settingsService);
        var wrapper = new CommandProviderWrapper(new TestCommandProvider(), TaskScheduler.Default);

        await wrapper.LoadTopLevelCommands(services);

        Assert.IsFalse(settingsService.Settings.ProviderSettings[ProviderId].FallbackCommands[FallbackId].IncludeInGlobalResults);
    }

    [TestMethod]
    public async Task DuplicateFallbackIds_KeepProviderIdentityAndGlobalSettings()
    {
        const string otherProviderId = "other-provider";
        var settingsService = new FakeSettingsService();
        settingsService.Settings = settingsService.Settings with
        {
            ProviderSettings = settingsService.Settings.ProviderSettings.SetItem(
                otherProviderId,
                new ProviderSettings
                {
                    FallbackCommands = ImmutableDictionary<string, FallbackSettings>.Empty.Add(
                        FallbackId,
                        new FallbackSettings { IncludeInGlobalResults = false }),
                }),
        };
        await using var services = CreateServices(settingsService);
        var first = new CommandProviderWrapper(new TestCommandProvider(), TaskScheduler.Default);
        var second = new CommandProviderWrapper(new TestCommandProvider(otherProviderId), TaskScheduler.Default);
        await first.LoadTopLevelCommands(services);
        await second.LoadTopLevelCommands(services);

        Assert.AreEqual($"{ProviderId}\0{FallbackId}", first.FallbackItems[0].FallbackKey);
        Assert.AreEqual($"{otherProviderId}\0{FallbackId}", second.FallbackItems[0].FallbackKey);
        Assert.IsTrue(first.FallbackItems[0].IncludeInGlobalResults);
        Assert.IsFalse(second.FallbackItems[0].IncludeInGlobalResults);
    }

    [TestMethod]
    public async Task FallbackRanking_PrefersProviderQualifiedIdAndPreservesLegacyRank()
    {
        var settingsService = new FakeSettingsService();
        await using var services = CreateServices(settingsService);
        var first = new CommandProviderWrapper(new TestCommandProvider(), TaskScheduler.Default);
        var second = new CommandProviderWrapper(new TestCommandProvider("other-provider"), TaskScheduler.Default);
        await first.LoadTopLevelCommands(services);
        await second.LoadTopLevelCommands(services);
        var score = typeof(MainListPage).GetMethod("ScoreFallbackItem", BindingFlags.NonPublic | BindingFlags.Static)!;
        string[] ranks = [second.FallbackItems[0].FallbackKey, first.FallbackItems[0].FallbackKey];

        Assert.AreEqual(3, score.Invoke(null, [second.FallbackItems[0], ranks]));
        Assert.AreEqual(2, score.Invoke(null, [first.FallbackItems[0], ranks]));
        Assert.AreEqual(2, score.Invoke(null, [first.FallbackItems[0], new[] { FallbackId }]));

        using var provider = new ProviderSettingsViewModel(first, new ProviderSettings(), settingsService);
        var fallback = new FallbackSettingsViewModel(first.FallbackItems[0], new FallbackSettings(), provider);
        Assert.AreEqual(first.FallbackItems[0].FallbackKey, fallback.RankId);
        Assert.AreEqual(FallbackId, fallback.Id);
    }

    [TestMethod]
    public async Task LoadTopLevelCommands_WhenFallbacksAreKnown_DoesNotWriteSettingsAgain()
    {
        var settingsService = new FakeSettingsService();
        await using var services = CreateServices(settingsService);
        await new CommandProviderWrapper(new TestCommandProvider(), TaskScheduler.Default).LoadTopLevelCommands(services);
        var writesAfterFirstLoad = settingsService.WriteCount;

        await new CommandProviderWrapper(new TestCommandProvider(), TaskScheduler.Default).LoadTopLevelCommands(services);

        // Only the save that happens before the fallbacks are loaded.
        Assert.AreEqual(writesAfterFirstLoad + 1, settingsService.WriteCount);
    }

    [DataTestMethod]
    [DataRow(true, true)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public async Task LoadTopLevelCommands_SavedProvider_WritesOnlyNewFallbackDefaults(bool isEnabled, bool hasSavedFallback)
    {
        var saved = new SettingsModel
        {
            ProviderSettings = ImmutableDictionary<string, ProviderSettings>.Empty.Add(
                ProviderId,
                new ProviderSettings(isEnabled)
                {
                    FallbackCommands = hasSavedFallback
                        ? ImmutableDictionary<string, FallbackSettings>.Empty.Add(FallbackId, new FallbackSettings(true))
                        : ImmutableDictionary<string, FallbackSettings>.Empty,
                }),
        };
        var persistence = new Mock<IPersistenceService>();
        persistence.Setup(p => p.Load(It.IsAny<string>(), JsonSerializationContext.Default.SettingsModel)).Returns(saved);
        persistence.Setup(p => p.Save(It.IsAny<SettingsModel>(), It.IsAny<string>(), JsonSerializationContext.Default.SettingsModel)).Returns(true);
        var appInfo = new Mock<IApplicationInfoService>();
        appInfo.Setup(a => a.ConfigDirectory).Returns(Path.Combine(Path.GetTempPath(), $"CmdPalTest_{Guid.NewGuid():N}"));
        var settingsService = new SettingsService(persistence.Object, appInfo.Object);
        await using var services = CreateServices(settingsService);

        for (var i = 0; i < 10; i++)
        {
            await new CommandProviderWrapper(new TestCommandProvider(), TaskScheduler.Default).LoadTopLevelCommands(services);
        }

        var connected = settingsService.Settings.ProviderSettings[ProviderId];
        Assert.AreEqual(isEnabled, connected.IsEnabled);
        Assert.AreEqual("Test provider", connected.ProviderDisplayName);
        Assert.IsTrue(connected.IsBuiltin);
        Assert.IsTrue(connected.FallbackCommands[FallbackId].IncludeInGlobalResults);
        persistence.Verify(
            p => p.Save(It.IsAny<SettingsModel>(), It.IsAny<string>(), JsonSerializationContext.Default.SettingsModel),
            hasSavedFallback ? Times.Never() : Times.Once());
    }

    private static ServiceProvider CreateServices(ISettingsService settingsService)
    {
        return new ServiceCollection()
            .AddSingleton(TaskScheduler.Default)
            .AddSingleton<ISettingsService>(settingsService)
            .BuildServiceProvider();
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public event TypedEventHandler<ISettingsService, SettingsModel>? SettingsChanged;

        public SettingsModel Settings { get; set; } = new();

        public int WriteCount { get; private set; }

        public void Save(bool hotReload = true) => WriteCount++;

        public void UpdateSettings(Func<SettingsModel, SettingsModel> transform, bool hotReload = true)
        {
            Settings = transform(Settings);
            WriteCount++;
            if (hotReload)
            {
                SettingsChanged?.Invoke(this, Settings);
            }
        }
    }

    private sealed partial class TestCommandProvider : CommandProvider
    {
        private readonly FallbackCommandItem _fallback = new(new NoOpCommand(), "Test fallback", FallbackId);

        public TestCommandProvider(string providerId = ProviderId)
        {
            Id = providerId;
            DisplayName = "Test provider";
        }

        public override ICommandItem[] TopLevelCommands() => [];

        public override IFallbackCommandItem[] FallbackCommands() => [_fallback];
    }
}
