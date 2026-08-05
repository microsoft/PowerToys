// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class MainListPageTests
{
    [TestMethod]
    public async Task CatalogChange_PreservesPublishedResultsWhileRescoring()
    {
        var settings = new SettingsModel();
        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(service => service.Settings).Returns(() => settings);
        settingsService.Setup(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((update, _) => settings = update(settings));
        using var services = new ServiceCollection()
            .AddSingleton(TaskScheduler.Default)
            .AddSingleton(settingsService.Object)
            .BuildServiceProvider();
        var wrapper = new CommandProviderWrapper(new AppsProvider(), TaskScheduler.Default);
        var extensionService = new Mock<IExtensionService>();
        extensionService.Setup(service => service.LoadProvidersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([wrapper]);
        using var manager = new TopLevelCommandManager(services, [extensionService.Object]);
        await manager.LoadExternalProvidersAsync();

        var original = CreateApp("Editor One");
        var snapshot = new AppListItemSnapshot([original], []);
        var source = new Mock<IAppListItemSource>();
        source.Setup(service => service.GetSnapshot()).Returns(() => snapshot);
        source.SetupGet(service => service.TopLevelResultLimit).Returns(10);
        var stateService = new Mock<IAppStateService>();
        stateService.SetupGet(service => service.State).Returns(new AppStateModel());
        var matcher = new PrecomputedFuzzyMatcher(new PrecomputedFuzzyMatcherOptions());
        var matcherProvider = new Mock<IFuzzyMatcherProvider>();
        var pauseScoring = 0;
        var scoringStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseScoring = new ManualResetEventSlim();
        matcherProvider.SetupGet(provider => provider.Current).Returns(() =>
        {
            if (Interlocked.Exchange(ref pauseScoring, 0) == 1)
            {
                scoringStarted.TrySetResult();
                Assert.IsTrue(releaseScoring.Wait(TimeSpan.FromSeconds(5)), "Scoring was not released.");
            }

            return matcher;
        });
        using var page = new MainListPage(manager, new AliasManager(manager, settingsService.Object), matcherProvider.Object, settingsService.Object, stateService.Object, source.Object);
        page.SearchText = "Editor";
        Assert.AreSame(original, page.GetItems().OfType<AppListItem>().Single());

        var added = CreateApp("Editor Two");
        snapshot = new AppListItemSnapshot([original, added], []);
        Interlocked.Exchange(ref pauseScoring, 1);
        source.Raise(service => service.Changed += null, source.Object, EventArgs.Empty);
        try
        {
            await scoringStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreSame(original, page.GetItems().OfType<AppListItem>().Single());
        }
        finally
        {
            releaseScoring.Set();
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (page.GetItems().OfType<AppListItem>().Count() != 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        CollectionAssert.AreEquivalent(new[] { original, added }, page.GetItems().OfType<AppListItem>().ToArray());
    }

    private static AppListItem CreateApp(string name) => new(
        new AppItem
        {
            Name = name,
            AppIdentifier = name,
            ExePath = $@"C:\Tools\{name}.exe",
        },
        useThumbnails: false);

    private sealed partial class AppsProvider : CommandProvider
    {
        public AppsProvider()
        {
            Id = AllAppsCommandProvider.WellKnownId;
            DisplayName = "Apps";
        }

        public override ICommandItem[] TopLevelCommands() => [];
    }
}
