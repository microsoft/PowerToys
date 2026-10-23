// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.Commands;
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
    [DataRow(FallbackCommandMode.Active)]
    [DataRow(FallbackCommandMode.Passive)]
    [DataRow(FallbackCommandMode.Results)]
    public async Task FallbackProvider_CreatesSpecializedTopLevelViewModel(FallbackCommandMode mode)
    {
        var fallback = new RecordingFallback(mode);
        await using var services = CreateServices();
        var source = await LoadFallbackAsync(fallback, services);
        try
        {
            Assert.IsInstanceOfType<TopLevelFallbackViewModel>(source);
            Assert.IsTrue(source.IsFallback);
            Assert.IsTrue(source.IsFallbackV2);
            Assert.AreEqual(mode, source.FallbackMode);
            Assert.AreEqual("fallback", source.Id);
            Assert.AreEqual("Test fallback", source.DisplayTitle);
            Assert.IsTrue(source.IsEnabled);
        }
        finally
        {
            source.Cleanup();
        }
    }

    [TestMethod]
    public async Task FallbackInitialization_PreservesPreparedQueryAndDoesNotReadAfterCleanup()
    {
        var fallback = new RecordingFallback(FallbackCommandMode.Passive) { Name = "Run" };
        await using var services = CreateServices();
        var source = await LoadFallbackAsync(fallback, services);
        try
        {
            PrepareQuery(source, FallbackCommandMode.Passive, CancellationToken.None);
            var invocation = source.CommandViewModel;
            fallback.FailNameRead = true;

            source.InitializeProperties();
            Assert.AreSame(invocation, source.CommandViewModel);

            source.Cleanup();
            source.InitializeProperties();
            Assert.IsNull(ShellViewModel.ResolveRequestedCommand(invocation));
            Assert.AreEqual(0, fallback.CreateCount);
        }
        finally
        {
            fallback.FailNameRead = false;
            source.Cleanup();
        }
    }

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
        var requested = message.Command;
        var invocation = requested as FallbackInvocationViewModel;
        Assert.IsNotNull(invocation);

        Assert.AreSame(fallback.Command, ShellViewModel.ResolveRequestedCommand(requested)?.Model.Unsafe);
        Assert.AreEqual(1, fallback.CreateCount);
        Assert.AreEqual("query", fallback.LastArgs?.Query);
        Assert.AreEqual("query-id", fallback.LastArgs?.QueryId);
        Assert.AreSame(source, invocation.QueryContext.InvocationContext);
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
        var requested = source.GetPerformCommandMessage().Command;
        cancellation.Cancel();

        Assert.IsNull(ShellViewModel.ResolveRequestedCommand(requested));
        Assert.AreEqual(0, fallback.CreateCount);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public async Task RenderedFallback_BorrowsInvocationAndTracksQueryChanges(int wrapperCount)
    {
        var fallback = new RecordingFallback(FallbackCommandMode.Passive);
        await using var services = CreateServices();
        var source = await LoadFallbackAsync(fallback, services);
        IListItem item = source;
        for (var i = 0; i < wrapperCount; i++)
        {
            item = new RecentCommandListItem(item, source.Id);
        }

        var rendered = new ListItemViewModel(item, source.ItemViewModel.PageContext, DefaultContextMenuFactory.Instance);
        try
        {
            rendered.SlowInitializeProperties();
            Assert.AreSame(source.CommandViewModel, rendered.Command);
            Assert.IsNull(ShellViewModel.ResolveRequestedCommand(rendered.Command));
            Assert.AreEqual(0, fallback.CreateCount);

            using var cancellation = new CancellationTokenSource();
            PrepareQuery(source, FallbackCommandMode.Passive, cancellation.Token);
            var firstMessage = new Messages.PerformCommandMessage(rendered);
            Assert.AreSame(source.CommandViewModel, firstMessage.Command);
            cancellation.Cancel();

            Assert.IsTrue(source.PreparePassiveFallback("next", "next-id", CancellationToken.None));
            var nextMessage = new Messages.PerformCommandMessage(rendered);
            Assert.AreNotSame(firstMessage.Command, nextMessage.Command);
            Assert.IsNull(ShellViewModel.ResolveRequestedCommand(firstMessage.Command));
            Assert.AreSame(fallback.Command, ShellViewModel.ResolveRequestedCommand(nextMessage.Command)?.Model.Unsafe);
            Assert.AreEqual(1, fallback.CreateCount);
            Assert.AreEqual("next", fallback.LastArgs?.Query);
            Assert.AreEqual("next-id", fallback.LastArgs?.QueryId);

            rendered.SafeCleanup();
            Assert.AreSame(fallback.Command, ShellViewModel.ResolveRequestedCommand(source.CommandViewModel)?.Model.Unsafe);
        }
        finally
        {
            rendered.SafeCleanup();
            source.Cleanup();
        }
    }

    [TestMethod]
    public async Task FallbackInvocation_RejectsQueryCanceledDuringCreation()
    {
        using var cancellation = new CancellationTokenSource();
        var fallback = new RecordingFallback(FallbackCommandMode.Passive) { OnCreate = cancellation.Cancel };
        await using var services = CreateServices();
        var source = await LoadFallbackAsync(fallback, services);
        PrepareQuery(source, FallbackCommandMode.Passive, cancellation.Token);

        Assert.IsNull(ShellViewModel.ResolveRequestedCommand(source.CommandViewModel));
        Assert.AreEqual(1, fallback.CreateCount);
    }

    [TestMethod]
    public async Task FallbackWithoutCommand_ExposesLazyPrimaryAction()
    {
        var command = new NoOpCommand { Id = "created.command" };
        var fallback = new RecordingFallback(FallbackCommandMode.Passive)
        {
            Command = null,
            Name = "Run",
            TitleTemplate = "Run {query}",
            CommandToCreate = command,
        };
        await using var services = CreateServices();
        var source = await LoadFallbackAsync(fallback, services);
        var rendered = new ListItemViewModel(source, source.ItemViewModel.PageContext, DefaultContextMenuFactory.Instance);
        try
        {
            rendered.SlowInitializeProperties();
            Assert.IsTrue(rendered.Command.IsSet);
            Assert.AreEqual("Run", rendered.Name);
            Assert.IsNotNull(rendered.PrimaryMenuItem);
            Assert.AreSame(source.CommandViewModel, rendered.PrimaryMenuItem.Command);
            Assert.AreEqual(0, fallback.CreateCount);

            PrepareQuery(source, FallbackCommandMode.Passive, CancellationToken.None);

            Assert.AreSame(source.CommandViewModel, rendered.PrimaryMenuItem.Command);
            Assert.AreSame(command, ShellViewModel.ResolveRequestedCommand(rendered.PrimaryMenuItem.Command)?.Model.Unsafe);
            Assert.AreEqual(1, fallback.CreateCount);
        }
        finally
        {
            rendered.SafeCleanup();
            source.Cleanup();
        }
    }

    [TestMethod]
    public async Task FallbackInvocation_TracksNameChangesAndRejectsCleanedUpSource()
    {
        var fallback = new RecordingFallback(FallbackCommandMode.Passive) { Name = "Run" };
        await using var services = CreateServices();
        var source = await LoadFallbackAsync(fallback, services);
        PrepareQuery(source, FallbackCommandMode.Passive, CancellationToken.None);
        var message = source.GetPerformCommandMessage();

        fallback.Name = "Updated action";
        source.ItemViewModel.ApplyPendingUpdates();
        Assert.AreEqual("Updated action", message.Command.Name);

        source.Cleanup();

        Assert.IsNull(ShellViewModel.ResolveRequestedCommand(message.Command));
        Assert.AreEqual(0, fallback.CreateCount);
    }

    [TestMethod]
    public async Task FallbackNameReadFailure_PreservesCachedNameAndForwardsNotification()
    {
        var fallback = new RecordingFallback(FallbackCommandMode.Passive) { Name = "Run" };
        await using var services = CreateServices();
        var source = await LoadFallbackAsync(fallback, services);
        try
        {
            PrepareQuery(source, FallbackCommandMode.Passive, CancellationToken.None);
            var invocation = source.CommandViewModel;
            var nameNotifications = 0;
            source.PropChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ICommand.Name))
                {
                    nameNotifications++;
                }
            };

            fallback.FailNameRead = true;
            fallback.NotifyNameChanged();
            source.ItemViewModel.ApplyPendingUpdates();

            Assert.AreEqual("Run", invocation.Name);
            Assert.IsTrue(nameNotifications > 0);

            fallback.FailNameRead = false;
            fallback.Name = "Recovered";
            source.ItemViewModel.ApplyPendingUpdates();
            Assert.AreEqual("Recovered", invocation.Name);
        }
        finally
        {
            fallback.FailNameRead = false;
            source.Cleanup();
        }
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
        : FallbackCommandItem(new NoOpCommand { Id = "command" }, "Test fallback", "fallback")
    {
        public override FallbackCommandMode Mode => mode;

        public bool FailNameRead { get; set; }

        public override string Name
        {
            get => FailNameRead ? throw new InvalidOperationException("Name read failed.") : base.Name;
            set => base.Name = value;
        }

        public void NotifyNameChanged() => OnPropertyChanged(nameof(Name));

        public int UpdateCount { get; private set; }

        public int CreateCount { get; private set; }

        public IFallbackCommandInvocationArgs? LastArgs { get; private set; }

        public Action? OnCreate { get; init; }

        public ICommand? CommandToCreate { get; init; }

        public override void UpdateQuery(string query) => UpdateCount++;

        public override ICommand CreateCommand(IFallbackCommandInvocationArgs args)
        {
            CreateCount++;
            LastArgs = args;
            OnCreate?.Invoke();
            return CommandToCreate ?? Command!;
        }
    }
}
