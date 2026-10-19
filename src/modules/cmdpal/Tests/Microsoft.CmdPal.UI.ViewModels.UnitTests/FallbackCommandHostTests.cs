// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class FallbackCommandHostTests
{
    [TestMethod]
    [DataRow("a.*", "abc", true)]
    [DataRow("abc", "xabc", false)]
    [DataRow(".", "ab", false)]
    [DataRow("(", "abc", false)]
    [DataRow("", "abc", false)]
    public async Task PassiveFallback_FormatsOnlyWholeQueryMatches(string pattern, string query, bool matches)
    {
        var fallback = new RecordingFallback(FallbackCommandMode.Passive)
        {
            MatchKind = HostMatchKind.Regex,
            MatchValue = pattern,
            TitleTemplate = "Open {query}",
            SubtitleTemplate = "Query: {query}",
        };
        await using var services = CreateServices();
        var source = await LoadFallbackAsync(fallback, services);

        Assert.AreEqual(matches, source.PreparePassiveFallback(query, "query-id", CancellationToken.None));
        Assert.AreEqual(matches ? $"Open {query}" : string.Empty, source.Title);
        Assert.AreEqual(matches ? $"Query: {query}" : string.Empty, source.Subtitle);
        Assert.AreEqual(0, fallback.UpdateCount);
        Assert.AreEqual(0, fallback.CreateCount);
    }

    [TestMethod]
    [DataRow(FallbackCommandMode.Active)]
    [DataRow(FallbackCommandMode.Passive)]
    public async Task FallbackInvocation_IsLazyAndPreservesQueryAndProvider(FallbackCommandMode mode)
    {
        var fallback = new RecordingFallback(mode);
        await using var services = CreateServices();
        var source = await LoadFallbackAsync(fallback, services);
        PrepareQuery(source, mode, CancellationToken.None);
        var message = source.GetPerformCommandMessage();
        Assert.AreEqual(0, fallback.CreateCount);
        var requested = message.Command.Unsafe!;
        var resolver = requested as IFallbackInvocationContext;
        Assert.IsNotNull(resolver);

        Assert.AreSame(fallback.Command, ShellViewModel.ResolveRequestedCommand(requested, resolver));
        Assert.AreEqual(1, fallback.CreateCount);
        Assert.AreEqual("query", fallback.LastArgs?.Query);
        Assert.AreEqual("query-id", fallback.LastArgs?.QueryId);
        Assert.AreSame(source, resolver.InvocationContext);
        var navigation = new PageNavigationService(
            Mock.Of<IPageViewModelFactoryService>(),
            Mock.Of<IAppHostService>(MockBehavior.Strict));
        Assert.AreSame(source.ExtensionHost, navigation.ResolveHost(message, null));
        Assert.AreSame(source.ProviderContext, navigation.ResolveProviderContext(message, null));
    }

    [TestMethod]
    [DataRow(FallbackCommandMode.Active)]
    [DataRow(FallbackCommandMode.Passive)]
    public async Task FallbackInvocation_RejectsCanceledQuery(FallbackCommandMode mode)
    {
        var fallback = new RecordingFallback(mode);
        await using var services = CreateServices();
        var source = await LoadFallbackAsync(fallback, services);
        using var cancellation = new CancellationTokenSource();
        PrepareQuery(source, mode, cancellation.Token);
        var requested = source.GetPerformCommandMessage().Command.Unsafe!;
        cancellation.Cancel();

        Assert.IsNull(ShellViewModel.ResolveRequestedCommand(requested, requested as IFallbackInvocationContext));
        Assert.AreEqual(0, fallback.CreateCount);
    }

    [TestMethod]
    public async Task ActiveFallback_PersistsSuggestedQuerySettings()
    {
        var fallback = new RecordingFallback(FallbackCommandMode.Active)
        {
            SuggestedQueryDelayMilliseconds = 125,
            SuggestedMinQueryLength = 3,
        };
        await using var services = CreateServices();
        var source = await LoadFallbackAsync(fallback, services);
        Assert.AreEqual(125u, source.EffectiveQueryDelayMilliseconds);
        Assert.AreEqual(3u, source.EffectiveMinimumQueryLength);
        var settings = services.GetRequiredService<ISettingsService>();
        settings.UpdateSettings(s => s with
        {
            ProviderSettings = s.ProviderSettings.SetItem(
                source.CommandProviderId,
                s.ProviderSettings[source.CommandProviderId] with
                {
                    FallbackCommands = s.ProviderSettings[source.CommandProviderId].FallbackCommands.SetItem(
                        source.Id,
                        new FallbackSettings { QueryDelayMilliseconds = 50, MinimumQueryLength = 1 }),
                }),
        });
        Assert.AreEqual(50u, source.EffectiveQueryDelayMilliseconds);
        Assert.AreEqual(1u, source.EffectiveMinimumQueryLength);
    }

    [TestMethod]
    public async Task ResultFallback_DoesNotUseLegacyQueryUpdates()
    {
        var fallback = new RecordingFallback(FallbackCommandMode.Results);
        await using var services = CreateServices();
        var source = await LoadFallbackAsync(fallback, services);
        var settings = services.GetRequiredService<ISettingsService>();
        using var manager = new TopLevelCommandManager(services, []);
        manager.TopLevelCommands.Add(source);
        var appState = new Mock<IAppStateService>();
        appState.SetupGet(s => s.State).Returns(new AppStateModel());
        using var page = new MainListPage(
            manager,
            new AliasManager(manager, settings),
            new FuzzyMatcherProvider(new PrecomputedFuzzyMatcherOptions()),
            settings,
            appState.Object,
            new ListPage());

        page.SearchText = "query";

        Assert.AreEqual(0, fallback.UpdateCount);
        Assert.IsFalse(page.GetItems().Contains(source));
    }

    private static void PrepareQuery(TopLevelViewModel source, FallbackCommandMode mode, CancellationToken token)
    {
        if (mode == FallbackCommandMode.Passive)
        {
            Assert.IsTrue(source.PreparePassiveFallback("query", "query-id", token));
        }
        else
        {
            source.SafeUpdateFallbackTextSynchronous("query", "query-id", token);
        }
    }

    private static ServiceProvider CreateServices()
    {
        var settings = new SettingsModel();
        var service = new Mock<ISettingsService>();
        service.SetupGet(s => s.Settings).Returns(() => settings);
        service.Setup(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((transform, _) => settings = transform(settings));
        return new ServiceCollection()
            .AddSingleton(TaskScheduler.Default)
            .AddSingleton(service.Object)
            .BuildServiceProvider();
    }

    private static async Task<TopLevelViewModel> LoadFallbackAsync(IFallbackCommandItem fallback, IServiceProvider services)
    {
        var wrapper = new CommandProviderWrapper(new TestProvider(fallback), TaskScheduler.Default);
        await wrapper.LoadTopLevelCommands(services);
        return wrapper.FallbackItems[0];
    }

    private sealed partial class TestProvider(IFallbackCommandItem fallback) : CommandProvider
    {
        public override string Id => "fallback-host-tests";

        public override ICommandItem[] TopLevelCommands() => [];

        public override IFallbackCommandItem[] FallbackCommands() => [fallback];
    }

    private sealed partial class RecordingFallback(FallbackCommandMode mode)
        : FallbackCommandItem3(new NoOpCommand { Id = "command" }, "Test fallback", "fallback")
    {
        public override FallbackCommandMode Mode => mode;

        public int UpdateCount { get; private set; }

        public int CreateCount { get; private set; }

        public IFallbackCommandInvocationArgs? LastArgs { get; private set; }

        public override void UpdateQuery(string query) => UpdateCount++;

        public override ICommand CreateCommand(IFallbackCommandInvocationArgs args)
        {
            CreateCount++;
            LastArgs = args;
            return Command!;
        }
    }
}
