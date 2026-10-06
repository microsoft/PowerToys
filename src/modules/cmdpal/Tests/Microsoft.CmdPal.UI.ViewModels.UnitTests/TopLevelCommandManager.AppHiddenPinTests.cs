// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class TopLevelCommandManagerTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task HiddenApp_HomeAndDockPreservePlacementAndExplicitRemoval(bool dockBand, bool patternHidden)
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"apps-hidden-pin-{Guid.NewGuid():N}.json");
        try
        {
            using var services = CreateServices();

            // Use the isolated-settings constructor without exposing Apps' generated native types to this assembly.
            var appsSettings = (AllAppsSettings)Activator.CreateInstance(
                typeof(AllAppsSettings),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: [settingsPath],
                culture: null)!;
            var app = new AppListItem(
                new AppItem { Name = "Portable Editor", Subtitle = "Edit documents", CatalogId = @"win32:E:\Apps\Editor.exe|args:", LaunchTarget = @"E:\Apps\Editor.exe" },
                useThumbnails: false);
            var id = app.Command!.Id.ToUpperInvariant();
            var snapshot = new AppListItemSnapshot([app], []);
            var source = new Mock<IAppListItemSource>();
            source.Setup(service => service.GetSnapshot()).Returns(() => Volatile.Read(ref snapshot));
            var matcherProvider = new FuzzyMatcherProvider(new(), new());
            using var page = new AllAppsPage(source.Object, matcherProvider);
            using var provider = new AllAppsCommandProvider(page, source.Object, appsSettings);
            var settingsService = services.GetRequiredService<ISettingsService>();
            var bandSettings = new DockBandSettings
            {
                ProviderId = provider.Id,
                CommandId = id,
                ShowTitles = false,
                ShowSubtitles = true,
            };
            settingsService.UpdateSettings(settings =>
            {
                var isolated = settings with
                {
                    DockSettings = settings.DockSettings with
                    {
                        StartBands = [],
                        CenterBands = [],
                        EndBands = dockBand ? [bandSettings] : [],
                    },
                };
                return dockBand ? isolated : isolated.TryPinCommand(provider.Id, id);
            });
            var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
            using var manager = new TopLevelCommandManager(services, [CreateExtensionService(wrapper).Object]);
            await manager.LoadExternalProvidersAsync();
            var stateService = new Mock<IAppStateService>();
            stateService.SetupGet(service => service.State).Returns(new AppStateModel());
            using var home = new MainListPage(
                manager,
                new AliasManager(manager, settingsService),
                matcherProvider,
                settingsService,
                stateService.Object,
                source.Object);

            TopLevelViewModel? GetSavedItem()
            {
                return dockBand ? manager.LookupDockBand(id) : manager.LookupCommand(provider.Id, id);
            }

            Assert.AreEqual(app.Title, GetSavedItem()?.Title);
            Assert.AreEqual(id, GetSavedItem()!.Id);
            AssertPlacement();

            var visible = GetSavedItem();
            Volatile.Write(ref snapshot, new AppListItemSnapshot([], patternHidden ? [] : [app], patternHidden ? [app] : []));
            source.Raise(service => service.Changed += null, source.Object, EventArgs.Empty);
            await WaitForHiddenPinAsync(() => GetSavedItem() is { } item && !ReferenceEquals(item, visible));
            Assert.AreEqual(app.Title, GetSavedItem()!.Title);
            Assert.AreEqual(id, GetSavedItem()!.Id);
            Assert.IsTrue(GetSavedItem()!.CommandViewModel.IsInvokableCommand);
            Assert.AreEqual(0, snapshot.VisibleItems.Count);
            AssertPlacement();

            if (!dockBand)
            {
                foreach (var query in new[] { app.Title, "documents" })
                {
                    home.SearchText = query;
                    Assert.IsTrue(home.GetItems().Any(item => item.Command?.Id == id), query);
                }
            }

            if (dockBand)
            {
                wrapper.UnpinDockBand(id, services, withReload: true);
            }
            else
            {
                wrapper.UnpinCommand(id, services);
            }

            await WaitForHiddenPinAsync(() => GetSavedItem() is null);
            Assert.IsFalse(settingsService.Settings.IsCommandPinned(provider.Id, id));
            Assert.AreEqual(0, settingsService.Settings.DockSettings.EndBands.Count);
            Assert.IsFalse(home.GetItems().Any(item => item.Command?.Id == id));

            void AssertPlacement()
            {
                if (dockBand)
                {
                    Assert.AreEqual(bandSettings, settingsService.Settings.DockSettings.EndBands[0]);
                    Assert.AreEqual(0, settingsService.Settings.DockSettings.StartBands.Count);
                    Assert.AreEqual(0, settingsService.Settings.DockSettings.CenterBands.Count);
                }
                else
                {
                    Assert.IsTrue(settingsService.Settings.IsCommandPinned(provider.Id, id));
                    Assert.AreEqual(1, settingsService.Settings.PinnedCommands.Count);
                    Assert.IsTrue(home.GetItems().Any(item => item.Command?.Id == id));
                }
            }
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(Path.ChangeExtension(settingsPath, ".aliases.json"));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ResolveCommandAsync_HiddenAppResolvesWithoutPinOrDockReference(bool patternHidden)
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"apps-hidden-link-{Guid.NewGuid():N}.json");
        try
        {
            using var services = CreateServices();
            var appsSettings = (AllAppsSettings)Activator.CreateInstance(
                typeof(AllAppsSettings),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: [settingsPath],
                culture: null)!;
            var app = new AppListItem(
                new AppItem { Name = "Hidden Editor", CatalogId = @"win32:E:\Apps\Editor.exe|args:", LaunchTarget = @"E:\Apps\Editor.exe" },
                useThumbnails: false);
            var id = app.Command!.Id.ToUpperInvariant();
            var snapshot = new AppListItemSnapshot([], patternHidden ? [] : [app], patternHidden ? [app] : []);
            var source = new Mock<IAppListItemSource>();
            source.Setup(service => service.GetSnapshot()).Returns(snapshot);
            using var page = new AllAppsPage(source.Object, new FuzzyMatcherProvider(new(), new()));
            using var provider = new AllAppsCommandProvider(page, source.Object, appsSettings);
            var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
            using var manager = new TopLevelCommandManager(services, [CreateExtensionService(wrapper).Object]);
            await manager.LoadExternalProvidersAsync();
            Assert.IsNull(manager.LookupCommand(provider.Id, id));
            Assert.IsNull(manager.LookupDockBand(id));

            using var resolution = await manager.ResolveCommandAsync(provider.Id, id);

            Assert.IsNotNull(resolution);
            Assert.AreEqual(id, resolution.Command.Id);
            Assert.AreEqual(app.Title, resolution.Command.Title);
            Assert.IsTrue(resolution.Command.CommandViewModel.IsInvokableCommand);
            Assert.IsNull(snapshot.GetVisibleApp(id));
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(Path.ChangeExtension(settingsPath, ".aliases.json"));
        }
    }

    private static async Task WaitForHiddenPinAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.IsTrue(condition(), "The saved app did not transition after the provider notification.");
    }
}
