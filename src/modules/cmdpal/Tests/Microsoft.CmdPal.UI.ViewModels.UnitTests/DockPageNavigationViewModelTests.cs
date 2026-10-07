// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveCards.ObjectModel.WinUI3;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Data.Json;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class DockPageNavigationViewModelTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private sealed class TestProviderContext(string providerId) : ICommandProviderContext
    {
        public string ProviderId { get; } = providerId;

        public bool SupportsPinning => true;
    }

    private sealed class TestAppHostService : IAppHostService
    {
        public AppExtensionHost GetDefaultHost() => new TestAppExtensionHost();

        public AppExtensionHost GetHostForCommand(object? context, AppExtensionHost? currentHost) =>
            currentHost ?? GetDefaultHost();

        public ICommandProviderContext GetProviderContextForCommand(object? command, ICommandProviderContext? currentContext) =>
            currentContext ?? CommandProviderContext.Empty;
    }

    private sealed class QueuedTaskScheduler : TaskScheduler
    {
        private readonly ConcurrentQueue<Task> _tasks = new();

        protected override IEnumerable<Task>? GetScheduledTasks() => _tasks.ToArray();

        protected override void QueueTask(Task task) => _tasks.Enqueue(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        public void RunAll()
        {
            while (_tasks.TryDequeue(out var task))
            {
                TryExecuteTask(task);
            }
        }
    }

    private sealed partial class TestContentPage : ContentPage
    {
        public override IContent[] GetContent() => [];
    }

    private sealed partial class TestParametersPage : ParametersPage
    {
        public override IListItem Command { get; } = new ListItem(new NoOpCommand { Name = "Run" });

        public override IParameterRun[] Parameters { get; } = [];
    }

    private sealed partial class TestListParameterPage : ParametersPage
    {
        public override IListItem Command { get; } = new ListItem(new NoOpCommand { Name = "Run" });

        public override IParameterRun[] Parameters { get; } = [new CommandParameterRun { Command = new ListPage() }];
    }

    private sealed partial class TestDynamicPage : DynamicListPage
    {
        private IListItem[] _items = [CreateItem("All")];

        public TaskCompletionSource<string> SearchUpdated { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? SearchAtFirstGetItems { get; private set; }

        public override IListItem[] GetItems()
        {
            SearchAtFirstGetItems ??= SearchText;
            return _items;
        }

        public override void UpdateSearchText(string oldSearch, string newSearch)
        {
            _items = [CreateItem(newSearch)];
            SearchUpdated.TrySetResult(newSearch);
            RaiseItemsChanged(_items.Length);
        }

        private static IListItem CreateItem(string title) =>
            new ListItem(new NoOpCommand { Name = title }) { Title = title };
    }

    private sealed partial class TestFormContent : FormContent
    {
        public override ICommandResult SubmitForm(string inputs, string data) => CommandResult.GoBack();
    }

    private sealed partial class BlockingPageViewModel(IPage page)
        : PageViewModel(page, TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty)
    {
        public ManualResetEventSlim Release { get; } = new();

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource CleanedUp { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void InitializeProperties()
        {
            Started.TrySetResult();
            Release.Wait(TimeSpan.FromSeconds(10));
        }

        protected override void UnsafeCleanup()
        {
            CleanedUp.TrySetResult();
            base.UnsafeCleanup();
        }
    }

    public sealed class ResultRecipient : IRecipient<HandleCommandResultMessage>
    {
        public TaskCompletionSource<HandleCommandResultMessage> MessageReceived { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Receive(HandleCommandResultMessage message) => MessageReceived.TrySetResult(message);
    }

    [TestMethod]
    public async Task NavigateAsync_KeepsSupportedPagesInOneRoute()
    {
        var route = NewRoute();
        var host = new TestAppExtensionHost();
        var providerContext = new TestProviderContext("provider");
        using var navigation = CreateNavigation(route);

        Assert.IsTrue(await navigation.NavigateAsync(CreateOwnerMessage(new ListPage { Name = "List" }, route, host, providerContext)));
        Assert.IsInstanceOfType<ListViewModel>(navigation.CurrentPage);
        Assert.IsTrue(navigation.CurrentPage.IsRootPage);
        Assert.IsFalse(navigation.CurrentPage.HasBackButton);
        Assert.AreEqual(route, navigation.CurrentPage.DockRoute);

        Assert.IsTrue(await navigation.NavigateAsync(CreatePageMessage(new TestContentPage { Name = "Content" }, navigation)));
        Assert.IsInstanceOfType<ContentPageViewModel>(navigation.CurrentPage);
        Assert.IsTrue(navigation.CanGoBack);
        Assert.IsTrue(navigation.CurrentPage.HasBackButton);

        Assert.IsTrue(await navigation.NavigateAsync(CreatePageMessage(new TestParametersPage { Name = "Parameters" }, navigation)));
        Assert.IsInstanceOfType<ParametersPageViewModel>(navigation.CurrentPage);
        Assert.AreEqual(2, navigation.BackStackDepth);

        Assert.IsTrue(await navigation.GoBackAsync());
        Assert.IsInstanceOfType<ContentPageViewModel>(navigation.CurrentPage);
        Assert.IsTrue(await navigation.GoBackAsync());
        Assert.IsInstanceOfType<ListViewModel>(navigation.CurrentPage);
        Assert.IsFalse(navigation.CanGoBack);
        Assert.IsFalse(await navigation.GoBackAsync());
    }

    [TestMethod]
    public async Task NavigateAsync_UsesTheOwnerFirstAndThenTheSendingPage()
    {
        var route = NewRoute();
        var ownerHost = new TestAppExtensionHost();
        var ownerProvider = new TestProviderContext("owner");
        using var navigation = CreateNavigation(route);

        Assert.IsTrue(await navigation.NavigateAsync(CreateOwnerMessage(new ListPage(), route, ownerHost, ownerProvider)));
        Assert.AreSame(ownerHost, navigation.CurrentPage!.ExtensionHost);
        Assert.AreSame(ownerProvider, navigation.CurrentPage.ProviderContext);

        Assert.IsTrue(await navigation.NavigateAsync(CreatePageMessage(new TestContentPage(), navigation)));
        Assert.AreSame(ownerHost, navigation.CurrentPage!.ExtensionHost);
        Assert.AreSame(ownerProvider, navigation.CurrentPage.ProviderContext);
    }

    [TestMethod]
    public async Task NavigateAsync_AppliesLaunchOptionsBeforeFirstFetch()
    {
        var route = NewRoute();
        var page = new TestDynamicPage();
        var message = CreateOwnerMessage(page, route, new TestAppExtensionHost(), CommandProviderContext.Empty);
        message.ListPageOptions = new(Query: "Result");
        using var navigation = CreateNavigation(route);

        Assert.IsTrue(await navigation.NavigateAsync(message));
        Assert.AreEqual("Result", page.SearchAtFirstGetItems);
        Assert.AreEqual("Result", navigation.CurrentPage!.SearchTextBox);
    }

    [TestMethod]
    public async Task NavigateAsync_InvalidLaunchOptions_DoesNotMutateNavigationState()
    {
        var route = NewRoute();
        using var navigation = CreateNavigation(route);
        Assert.IsTrue(await navigation.NavigateAsync(CreateOwnerMessage(new ListPage(), route, new TestAppExtensionHost(), CommandProviderContext.Empty)));
        var currentPage = navigation.CurrentPage;
        var message = CreatePageMessage(new TestContentPage(), navigation);
        message.ListPageOptions = new(Query: "query");

        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => navigation.NavigateAsync(message));

        Assert.AreSame(currentPage, navigation.CurrentPage);
        Assert.AreEqual(0, navigation.BackStackDepth);
        Assert.IsTrue(await navigation.NavigateAsync(CreatePageMessage(new ListPage(), navigation)));
        Assert.AreEqual(1, navigation.BackStackDepth);
    }

    [TestMethod]
    public async Task NavigateAsync_RejectsAnotherDockRequest()
    {
        var route = NewRoute();
        using var navigation = CreateNavigation(route);
        var otherRoute = new DockCommandRoute((nint)43, Guid.NewGuid());

        var navigated = await navigation.NavigateAsync(
            CreateOwnerMessage(new ListPage { Name = "List" }, otherRoute, new TestAppExtensionHost(), CommandProviderContext.Empty));

        Assert.IsFalse(navigated);
        Assert.IsNull(navigation.CurrentPage);
    }

    [TestMethod]
    public async Task NavigateAsync_OnlyTheCurrentPageCanPushAnotherPage()
    {
        var route = NewRoute();
        var host = new TestAppExtensionHost();
        using var navigation = CreateNavigation(route);
        Assert.IsTrue(await navigation.NavigateAsync(CreateOwnerMessage(new ListPage(), route, host, CommandProviderContext.Empty)));
        var root = navigation.CurrentPage!;
        Assert.IsTrue(await navigation.NavigateAsync(CreatePageMessage(new TestContentPage(), navigation)));
        var current = navigation.CurrentPage;

        Assert.IsFalse(await navigation.NavigateAsync(new PerformCommandMessage(new ExtensionObject<ICommand>(new ListPage()), root)));
        Assert.IsFalse(await navigation.NavigateAsync(CreateOwnerMessage(new ListPage(), route, host, CommandProviderContext.Empty)));

        Assert.AreSame(current, navigation.CurrentPage);
        Assert.AreEqual(1, navigation.BackStackDepth);
    }

    [TestMethod]
    public async Task NavigateAsync_AfterDispose_ReturnsFalse()
    {
        var route = NewRoute();
        var navigation = CreateNavigation(route);
        navigation.Dispose();

        Assert.IsFalse(await navigation.NavigateAsync(CreateOwnerMessage(new ListPage(), route, new TestAppExtensionHost(), CommandProviderContext.Empty)));
        Assert.IsFalse(await navigation.GoBackAsync());
        Assert.IsFalse(await navigation.GoHomeAsync());
        Assert.IsNull(navigation.CurrentPage);
    }

    [TestMethod]
    public async Task Dispose_DuringInitialization_ReleasesThePageAfterTheExtensionCallReturns()
    {
        var route = NewRoute();
        var page = new BlockingPageViewModel(new ListPage());
        var factory = new Mock<IPageViewModelFactoryService>();
        factory
            .Setup(f => f.TryCreatePageViewModel(It.IsAny<IPage>(), It.IsAny<bool>(), It.IsAny<AppExtensionHost>(), It.IsAny<ICommandProviderContext>()))
            .Returns(page);
        var navigation = CreateNavigation(route, factory: factory.Object);

        try
        {
            var navigate = navigation.NavigateAsync(CreateOwnerMessage(new ListPage(), route, new TestAppExtensionHost(), CommandProviderContext.Empty));
            await page.Started.Task.WaitAsync(WaitTimeout);

            navigation.Dispose();

            Assert.IsNull(navigation.CurrentPage);
            Assert.IsFalse(page.CleanedUp.Task.IsCompleted);
            page.Release.Set();
            await page.CleanedUp.Task.WaitAsync(WaitTimeout);
            await navigate.WaitAsync(WaitTimeout);
        }
        finally
        {
            page.Release.Set();
            page.Release.Dispose();
        }
    }

    [TestMethod]
    public async Task DynamicListSearch_UpdatesTheRoutedPageItems()
    {
        var route = NewRoute();
        var page = new TestDynamicPage
        {
            Name = "Dynamic",
            PlaceholderText = "Find an item",
        };
        using var navigation = CreateNavigation(route);

        Assert.IsTrue(await navigation.NavigateAsync(CreateOwnerMessage(page, route, new TestAppExtensionHost(), CommandProviderContext.Empty)));

        var list = (ListViewModel)navigation.CurrentPage!;
        var itemsUpdated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        list.ItemsUpdated += (_, _) =>
        {
            if (list.FilteredItems.SingleOrDefault()?.Title == "Result")
            {
                itemsUpdated.TrySetResult();
            }
        };

        list.SearchTextBox = "Result";

        Assert.AreEqual("Result", await page.SearchUpdated.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        await itemsUpdated.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual("Find an item", list.PlaceholderText);
        Assert.AreEqual("Result", list.FilteredItems.Single().Title);
    }

    [TestMethod]
    public async Task GoHomeAsync_ReturnsToTheDockRoot()
    {
        var route = NewRoute();
        using var navigation = CreateNavigation(route);

        Assert.IsTrue(await navigation.NavigateAsync(CreateOwnerMessage(new ListPage(), route, new TestAppExtensionHost(), CommandProviderContext.Empty)));
        Assert.IsTrue(await navigation.NavigateAsync(CreatePageMessage(new TestContentPage(), navigation)));
        Assert.IsTrue(await navigation.NavigateAsync(CreatePageMessage(new TestParametersPage(), navigation)));

        Assert.IsTrue(await navigation.GoHomeAsync());
        Assert.IsInstanceOfType<ListViewModel>(navigation.CurrentPage);
        Assert.AreEqual(0, navigation.BackStackDepth);
        Assert.IsFalse(navigation.CanGoBack);
    }

    [TestMethod]
    public async Task Messages_FromADockPage_CarryItsRouteAndContext()
    {
        var route = NewRoute();
        var host = new TestAppExtensionHost();
        var providerContext = new TestProviderContext("provider");
        using var navigation = CreateNavigation(route);
        Assert.IsTrue(await navigation.NavigateAsync(CreateOwnerMessage(new TestContentPage(), route, host, providerContext)));
        var sourcePage = navigation.CurrentPage!;

        var result = new HandleCommandResultMessage(new(CommandResult.KeepOpen()), sourcePage);
        var command = new PerformCommandMessage(new ExtensionObject<ICommand>(new NoOpCommand()), sourcePage);

        Assert.AreEqual(route, result.DockRoute);
        Assert.AreSame(sourcePage, result.Context?.Page);
        Assert.AreSame(host, result.Context?.ExtensionHost);
        Assert.AreSame(providerContext, result.Context?.ProviderContext);
        Assert.AreEqual(route, command.DockRoute);
        Assert.AreSame(sourcePage, command.Context?.Page);
        Assert.AreSame(host, command.Context?.ExtensionHost);
        Assert.AreSame(providerContext, command.Context?.ProviderContext);
    }

    [TestMethod]
    public async Task ContentFormSubmit_SendsTheSourceDockRoute()
    {
        var route = NewRoute();
        var host = new TestAppExtensionHost();
        var providerContext = new TestProviderContext("provider");
        var sourcePage = new PageViewModel(new TestContentPage(), TaskScheduler.Default, host, providerContext)
        {
            DockRoute = route,
        };
        var form = new ContentFormViewModel(new TestFormContent(), new(sourcePage));
        var recipient = new ResultRecipient();
        WeakReferenceMessenger.Default.Register<HandleCommandResultMessage>(recipient);

        try
        {
            form.HandleSubmit(new AdaptiveExecuteAction(), new JsonObject());
            var message = await recipient.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.AreEqual(CommandResultKind.GoBack, message.Result.Unsafe!.Kind);
            Assert.AreEqual(route, message.DockRoute);
            Assert.AreSame(sourcePage, message.Context?.Page);
            Assert.AreSame(host, message.Context?.ExtensionHost);
            Assert.AreSame(providerContext, message.Context?.ProviderContext);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            form.SafeCleanup();
            sourcePage.SafeCleanup();
        }
    }

    [TestMethod]
    public async Task OwnsSourcePage_AcceptsOnlyTheCurrentParametersList()
    {
        var route = NewRoute();
        using var navigation = CreateNavigation(route);
        Assert.IsTrue(await navigation.NavigateAsync(
            CreateOwnerMessage(new TestListParameterPage(), route, new TestAppExtensionHost(), new TestProviderContext("provider"))));

        var parameters = (ParametersPageViewModel)navigation.CurrentPage!;
        await WaitUntilAsync(() => parameters.Items.OfType<CommandParameterRunViewModel>().Any());
        var run = parameters.Items.OfType<CommandParameterRunViewModel>().Single();
        var activeList = run.ListViewModel;
        Assert.IsNotNull(activeList);
        Assert.AreEqual(route, activeList.DockRoute);

        parameters.SetActiveListParameter(run);
        Assert.IsTrue(navigation.OwnsSourcePage(activeList));

        parameters.SetActiveListParameter(null);
        Assert.IsFalse(navigation.OwnsSourcePage(activeList));

        parameters.SetActiveListParameter(run);
        Assert.IsTrue(await navigation.NavigateAsync(CreatePageMessage(new TestContentPage(), navigation)));
        Assert.IsFalse(navigation.OwnsSourcePage(activeList));
    }

    [TestMethod]
    public async Task HandleCommandResult_ToastsAndConfirmationsAlwaysGoToTheShell()
    {
        var route = NewRoute();
        var navigation = CreateNavigation(route);
        Assert.IsTrue(await navigation.NavigateAsync(CreateOwnerMessage(new ListPage(), route, new TestAppExtensionHost(), CommandProviderContext.Empty)));
        var page = navigation.CurrentPage!;
        ICommandResult[] results =
        [
            CommandResult.ShowToast("Done"),
            CommandResult.Confirm(new ConfirmationArgs { Title = "Sure?", PrimaryCommand = new NoOpCommand() }),
        ];

        foreach (var result in results)
        {
            Assert.IsFalse(navigation.HandleCommandResult(page, result));
        }

        // The flyout closing while the command runs must not swallow these.
        navigation.Dispose();
        foreach (var result in results)
        {
            Assert.IsFalse(navigation.HandleCommandResult(page, result));
        }
    }

    [TestMethod]
    public async Task HandleCommandResult_AppliesNavigationOnTheUiScheduler()
    {
        var route = NewRoute();
        var scheduler = new QueuedTaskScheduler();
        using var navigation = CreateNavigation(route, scheduler);
        Assert.IsTrue(await DrainUntilCompleteAsync(
            scheduler,
            navigation.NavigateAsync(CreateOwnerMessage(new ListPage(), route, new TestAppExtensionHost(), CommandProviderContext.Empty))));
        Assert.IsTrue(await DrainUntilCompleteAsync(scheduler, navigation.NavigateAsync(CreatePageMessage(new TestContentPage(), navigation))));
        var source = navigation.CurrentPage!;

        Assert.IsTrue(navigation.HandleCommandResult(source, CommandResult.GoBack()));
        Assert.AreSame(source, navigation.CurrentPage);

        await DrainUntilAsync(scheduler, () => !ReferenceEquals(source, navigation.CurrentPage));
        Assert.IsInstanceOfType<ListViewModel>(navigation.CurrentPage);
        Assert.IsFalse(navigation.CanGoBack);
    }

    [TestMethod]
    [DataRow(CommandResultKind.Dismiss)]
    [DataRow(CommandResultKind.Hide)]
    [DataRow(CommandResultKind.GoBack)]
    public async Task HandleCommandResult_ClosesTheFlyout(CommandResultKind kind)
    {
        var route = NewRoute();
        using var navigation = CreateNavigation(route);
        Assert.IsTrue(await navigation.NavigateAsync(CreateOwnerMessage(new ListPage(), route, new TestAppExtensionHost(), CommandProviderContext.Empty)));
        var closeRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        navigation.CloseRequested += (_, _) => closeRequested.TrySetResult();

        Assert.IsTrue(navigation.HandleCommandResult(navigation.CurrentPage!, CreateResult(kind)));

        await closeRequested.Task.WaitAsync(WaitTimeout);
    }

    [TestMethod]
    public async Task HandleCommandResult_GoHomeReturnsToTheDockRoot()
    {
        var route = NewRoute();
        using var navigation = CreateNavigation(route);
        Assert.IsTrue(await navigation.NavigateAsync(CreateOwnerMessage(new ListPage(), route, new TestAppExtensionHost(), CommandProviderContext.Empty)));
        Assert.IsTrue(await navigation.NavigateAsync(CreatePageMessage(new TestContentPage(), navigation)));
        Assert.IsTrue(await navigation.NavigateAsync(CreatePageMessage(new TestParametersPage(), navigation)));

        Assert.IsTrue(navigation.HandleCommandResult(navigation.CurrentPage!, CommandResult.GoHome()));

        await WaitUntilAsync(() => navigation.BackStackDepth == 0);
        Assert.IsInstanceOfType<ListViewModel>(navigation.CurrentPage);
    }

    [TestMethod]
    public async Task HandleCommandResult_DropsResultsFromAPageThatIsNoLongerCurrent()
    {
        var route = NewRoute();
        var scheduler = new QueuedTaskScheduler();
        var navigation = CreateNavigation(route, scheduler);
        Assert.IsTrue(await DrainUntilCompleteAsync(
            scheduler,
            navigation.NavigateAsync(CreateOwnerMessage(new ListPage(), route, new TestAppExtensionHost(), CommandProviderContext.Empty))));
        var root = navigation.CurrentPage!;
        Assert.IsTrue(await DrainUntilCompleteAsync(scheduler, navigation.NavigateAsync(CreatePageMessage(new TestContentPage(), navigation))));
        var current = navigation.CurrentPage!;
        var closeRequests = 0;
        navigation.CloseRequested += (_, _) => closeRequests++;

        Assert.IsTrue(navigation.HandleCommandResult(root, CommandResult.Dismiss()));
        Assert.IsTrue(navigation.HandleCommandResult(root, CommandResult.GoBack()));
        scheduler.RunAll();

        Assert.AreSame(current, navigation.CurrentPage);
        Assert.AreEqual(0, closeRequests);
        navigation.Dispose();
    }

    private static DockCommandRoute NewRoute() => new((nint)42, Guid.NewGuid());

    private static DockPageNavigationViewModel CreateNavigation(
        DockCommandRoute route,
        TaskScheduler? scheduler = null,
        IPageViewModelFactoryService? factory = null) =>
        new(
            route,
            scheduler ?? TaskScheduler.Default,
            factory ?? new CommandPalettePageViewModelFactory(TaskScheduler.Default, DefaultContextMenuFactory.Instance),
            new TestAppHostService());

    // The dock's first request: it has an owning provider, but no source page.
    private static PerformCommandMessage CreateOwnerMessage(
        IPage page,
        DockCommandRoute route,
        AppExtensionHost host,
        ICommandProviderContext providerContext) =>
        new(new ExtensionObject<ICommand>(page))
        {
            Context = new SourceContext(host, providerContext),
            DockRoute = route,
        };

    // A request sent by the page the flyout currently shows.
    private static PerformCommandMessage CreatePageMessage(IPage page, DockPageNavigationViewModel navigation) =>
        new(new ExtensionObject<ICommand>(page), navigation.CurrentPage);

    private static CommandResult CreateResult(CommandResultKind kind) =>
        kind switch
        {
            CommandResultKind.Dismiss => CommandResult.Dismiss(),
            CommandResultKind.Hide => CommandResult.Hide(),
            CommandResultKind.GoBack => CommandResult.GoBack(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the condition.");
            }

            await Task.Delay(10);
        }
    }

    private static async Task DrainUntilAsync(QueuedTaskScheduler scheduler, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;
        while (true)
        {
            scheduler.RunAll();
            if (condition())
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the condition.");
            }

            await Task.Delay(10);
        }
    }

    private static async Task<T> DrainUntilCompleteAsync<T>(QueuedTaskScheduler scheduler, Task<T> task)
    {
        await DrainUntilAsync(scheduler, () => task.IsCompleted);
        return await task;
    }
}
