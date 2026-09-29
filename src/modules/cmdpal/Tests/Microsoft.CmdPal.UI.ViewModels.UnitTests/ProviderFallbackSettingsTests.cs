// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        CollectionAssert.Contains(settingsService.Settings.GetGlobalFallbacks(), FallbackId);
    }

    [TestMethod]
    public async Task LoadTopLevelCommands_KeepsSavedFallbackSettings()
    {
        var settingsService = new FakeSettingsService();
        var saved = new ProviderSettings
        {
            FallbackCommands = ImmutableDictionary<string, FallbackSettings>.Empty.Add(FallbackId, new FallbackSettings(isEnabled: true, includeInGlobalResults: false)),
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

    private static ServiceProvider CreateServices(FakeSettingsService settingsService)
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

        public TestCommandProvider()
        {
            Id = ProviderId;
            DisplayName = "Test provider";
        }

        public override ICommandItem[] TopLevelCommands() => [];

        public override IFallbackCommandItem[] FallbackCommands() => [_fallback];
    }
}
