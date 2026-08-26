// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using AdaptiveCards.ObjectModel.WinUI3;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Data.Json;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class DockPageNavigationViewModelTests
{
    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

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

    private sealed partial class TestContentPage : ContentPage
    {
        public override IContent[] GetContent() => [];
    }

    private sealed partial class TestParametersPage : ParametersPage
    {
        public override IListItem Command { get; } = new ListItem(new NoOpCommand { Name = "Run" });

        public override IParameterRun[] Parameters { get; } = [];
    }

    private sealed partial class TestDynamicPage : DynamicListPage
    {
        private IListItem[] _items = [CreateItem("All")];

        public TaskCompletionSource<string> SearchUpdated { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override IListItem[] GetItems() => _items;

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

    public sealed class ResultRecipient : IRecipient<HandleCommandResultMessage>
    {
        public TaskCompletionSource<HandleCommandResultMessage> MessageReceived { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Receive(HandleCommandResultMessage message) => MessageReceived.TrySetResult(message);
    }

    [TestMethod]
    public async Task NavigateAsync_KeepsSupportedPagesInOneRoute()
    {
        var route = new DockCommandRoute((nint)42, Guid.NewGuid());
        var host = new TestAppExtensionHost();
        var providerContext = new TestProviderContext("provider");
        using var navigation = CreateNavigation(route);

        var list = CreateMessage(new ListPage { Name = "List" }, route, host, providerContext);
        Assert.IsTrue(await navigation.NavigateAsync(list));
        Assert.IsInstanceOfType<ListViewModel>(navigation.CurrentPage);
        Assert.IsTrue(navigation.CurrentPage.IsRootPage);
        Assert.AreEqual(route, navigation.CurrentPage.DockRoute);
        Assert.AreSame(host, navigation.CurrentPage.ExtensionHost);
        Assert.AreSame(providerContext, navigation.CurrentPage.ProviderContext);

        var content = CreateMessage(new TestContentPage { Name = "Content" }, route, host, providerContext);
        Assert.IsTrue(await navigation.NavigateAsync(content));
        Assert.IsInstanceOfType<ContentPageViewModel>(navigation.CurrentPage);
        Assert.IsTrue(navigation.CanGoBack);
        Assert.IsTrue(navigation.CurrentPage.HasBackButton);

        var parameters = CreateMessage(new TestParametersPage { Name = "Parameters" }, route, host, providerContext);
        Assert.IsTrue(await navigation.NavigateAsync(parameters));
        Assert.IsInstanceOfType<ParametersPageViewModel>(navigation.CurrentPage);
        Assert.AreEqual(2, navigation.BackStackDepth);

        Assert.IsTrue(await navigation.GoBackAsync());
        Assert.IsInstanceOfType<ContentPageViewModel>(navigation.CurrentPage);
        Assert.IsTrue(await navigation.GoBackAsync());
        Assert.IsInstanceOfType<ListViewModel>(navigation.CurrentPage);
        Assert.IsFalse(navigation.CanGoBack);
    }

    [TestMethod]
    public async Task NavigateAsync_RejectsAnotherDockRequest()
    {
        var route = new DockCommandRoute((nint)42, Guid.NewGuid());
        using var navigation = CreateNavigation(route);
        var otherRoute = new DockCommandRoute((nint)43, Guid.NewGuid());

        var navigated = await navigation.NavigateAsync(
            CreateMessage(
                new ListPage { Name = "List" },
                otherRoute,
                new TestAppExtensionHost(),
                CommandProviderContext.Empty));

        Assert.IsFalse(navigated);
        Assert.IsNull(navigation.CurrentPage);
    }

    [TestMethod]
    public async Task DynamicListSearch_UpdatesTheRoutedPageItems()
    {
        var route = new DockCommandRoute((nint)42, Guid.NewGuid());
        var page = new TestDynamicPage
        {
            Name = "Dynamic",
            PlaceholderText = "Find an item",
        };
        using var navigation = CreateNavigation(route);

        Assert.IsTrue(
            await navigation.NavigateAsync(
                CreateMessage(
                    page,
                    route,
                    new TestAppExtensionHost(),
                    CommandProviderContext.Empty)));

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
        var route = new DockCommandRoute((nint)42, Guid.NewGuid());
        var host = new TestAppExtensionHost();
        var providerContext = new TestProviderContext("provider");
        using var navigation = CreateNavigation(route);

        Assert.IsTrue(await navigation.NavigateAsync(CreateMessage(new ListPage(), route, host, providerContext)));
        Assert.IsTrue(await navigation.NavigateAsync(CreateMessage(new TestContentPage(), route, host, providerContext)));
        Assert.IsTrue(await navigation.NavigateAsync(CreateMessage(new TestParametersPage(), route, host, providerContext)));

        Assert.IsTrue(await navigation.GoHomeAsync());
        Assert.IsInstanceOfType<ListViewModel>(navigation.CurrentPage);
        Assert.AreEqual(0, navigation.BackStackDepth);
        Assert.IsFalse(navigation.CanGoBack);
    }

    [TestMethod]
    public async Task ResultMessage_PreservesTheDockRouteAndSourcePage()
    {
        var route = new DockCommandRoute((nint)42, Guid.NewGuid());
        var host = new TestAppExtensionHost();
        var providerContext = new TestProviderContext("provider");
        using var navigation = CreateNavigation(route);
        Assert.IsTrue(await navigation.NavigateAsync(CreateMessage(new TestContentPage(), route, host, providerContext)));

        var sourcePage = navigation.CurrentPage!;
        var message = sourcePage.PrepareHandleCommandResultMessage(
            new HandleCommandResultMessage(new(CommandResult.KeepOpen())));

        Assert.AreEqual(route, message.DockRoute);
        Assert.AreSame(sourcePage, message.SourcePage);
        Assert.AreSame(host, message.SourceExtensionHost);
        Assert.AreSame(providerContext, message.SourceProviderContext);

        var commandMessage = sourcePage.PreparePerformCommandMessage(
            new PerformCommandMessage(new ExtensionObject<ICommand>(new NoOpCommand())));
        Assert.AreEqual(route, commandMessage.DockRoute);
        Assert.AreSame(sourcePage, commandMessage.SourcePage);
        Assert.AreSame(host, commandMessage.SourceExtensionHost);
        Assert.AreSame(providerContext, commandMessage.SourceProviderContext);
    }

    [TestMethod]
    public async Task ContentFormSubmit_SendsTheSourceDockRoute()
    {
        var route = new DockCommandRoute((nint)42, Guid.NewGuid());
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
            Assert.AreSame(sourcePage, message.SourcePage);
            Assert.AreSame(host, message.SourceExtensionHost);
            Assert.AreSame(providerContext, message.SourceProviderContext);
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
        var route = new DockCommandRoute((nint)42, Guid.NewGuid());
        var host = new TestAppExtensionHost();
        var providerContext = new TestProviderContext("provider");
        using var navigation = CreateNavigation(route);
        Assert.IsTrue(
            await navigation.NavigateAsync(
                CreateMessage(new TestParametersPage(), route, host, providerContext)));

        var parameters = (ParametersPageViewModel)navigation.CurrentPage!;
        var activeList = new ListViewModel(
            new ListPage(),
            TaskScheduler.Default,
            host,
            providerContext,
            DefaultContextMenuFactory.Instance)
        {
            DockRoute = route,
        };

        try
        {
            parameters.ActiveListViewModel = activeList;
            Assert.IsTrue(navigation.OwnsSourcePage(activeList));

            parameters.ActiveListViewModel = null;
            Assert.IsFalse(navigation.OwnsSourcePage(activeList));

            parameters.ActiveListViewModel = activeList;
            Assert.IsTrue(
                await navigation.NavigateAsync(
                    CreateMessage(new TestContentPage(), route, host, providerContext)));
            Assert.IsFalse(navigation.OwnsSourcePage(activeList));
        }
        finally
        {
            activeList.SafeCleanup();
            activeList.Dispose();
        }
    }

    private static DockPageNavigationViewModel CreateNavigation(DockCommandRoute route) =>
        new(
            route,
            TaskScheduler.Default,
            new CommandPalettePageViewModelFactory(TaskScheduler.Default, DefaultContextMenuFactory.Instance),
            new TestAppHostService());

    private static PerformCommandMessage CreateMessage(
        IPage page,
        DockCommandRoute route,
        AppExtensionHost host,
        ICommandProviderContext providerContext) =>
        new(new ExtensionObject<ICommand>(page))
        {
            DockRoute = route,
            SourceExtensionHost = host,
            SourceProviderContext = providerContext,
        };
}
