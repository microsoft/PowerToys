// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed partial class PageCleanupTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task CleanupReturnsBeforeAnExtensionUnsubscribeFinishes()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = CreateListPage();
        var viewModel = CreateListViewModel(page.Object);
        viewModel.InitializeProperties();
        page.SetupRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback(() => Block(entered, release));

        var cleanup = viewModel.CleanupAsync();
        try
        {
            await entered.Task.WaitAsync(TestTimeout);
            Assert.IsTrue(viewModel.IsDiscarded);
            Assert.IsFalse(cleanup.IsCompleted);
            Assert.AreSame(cleanup, viewModel.CleanupAsync());
            await viewModel.ResumeAfterNavigation().WaitAsync(TestTimeout);
            page.Verify(p => p.GetItems(), Times.Once);
        }
        finally
        {
            release.Set();
            await cleanup.WaitAsync(TestTimeout);
        }

        page.VerifyRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
    }

    [DataTestMethod]
    [DataRow("Id")]
    [DataRow("GetItems")]
    [DataRow("ItemsChanged")]
    public async Task CleanupWaitsForLateInitializationWithoutBlockingNavigation(string blockedCall)
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = CreateListPage();
        switch (blockedCall)
        {
            case "Id":
                page.SetupGet(p => p.Id).Returns(() =>
                {
                    Block(entered, release);
                    return "page";
                });
                break;
            case "GetItems":
                page.Setup(p => p.GetItems()).Returns(() =>
                {
                    Block(entered, release);
                    return [];
                });
                break;
            case "ItemsChanged":
                page.SetupAdd(p => p.ItemsChanged += It.IsAny<TypedEventHandler<object, IItemsChangedEventArgs>>())
                    .Callback(() => Block(entered, release));
                break;
        }

        var viewModel = CreateListViewModel(page.Object);
        var initialization = Task.Run(viewModel.InitializeProperties);
        Task? cleanup = null;
        try
        {
            await entered.Task.WaitAsync(TestTimeout);
            cleanup = viewModel.CleanupAsync();
            Assert.IsTrue(viewModel.IsDiscarded);
            Assert.IsFalse(cleanup.IsCompleted);
            page.VerifyRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Never);
        }
        finally
        {
            release.Set();
            await initialization.WaitAsync(TestTimeout);
            await (cleanup ?? viewModel.CleanupAsync()).WaitAsync(TestTimeout);
        }

        page.VerifyAdd(p => p.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
        page.VerifyAdd(p => p.ItemsChanged += It.IsAny<TypedEventHandler<object, IItemsChangedEventArgs>>(), Times.Once);
        page.VerifyRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
        page.VerifyRemove(p => p.ItemsChanged -= It.IsAny<TypedEventHandler<object, IItemsChangedEventArgs>>(), Times.Once);
    }

    [TestMethod]
    public async Task DiscardedPageIgnoresQueuedNotificationsAndReinitialization()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new Mock<IPage>();
        TypedEventHandler<object, IPropChangedEventArgs>? queuedNotification = null;
        page.SetupAdd(p => p.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback<TypedEventHandler<object, IPropChangedEventArgs>>(handler => queuedNotification = handler);
        var viewModel = new PageViewModel(page.Object, scheduler, new TestHost(), CommandProviderContext.Empty);
        await viewModel.InitializeAsync();
        await viewModel.CleanupAsync().WaitAsync(TestTimeout);

        Assert.IsNotNull(queuedNotification);
        queuedNotification(page.Object, new PropChangedEventArgs(nameof(IPage.Title)));
        viewModel.InitializeProperties();
        scheduler.Drain();

        Assert.IsFalse(viewModel.IsInitialized);
        page.VerifyGet(p => p.Title, Times.Once);
        page.VerifyAdd(p => p.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
    }

    [TestMethod]
    public async Task ContentWaitingForUiPublicationIsReleasedAfterDiscard()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new Mock<IContentPage>();
        page.Setup(p => p.GetContent()).Returns([Mock.Of<IContent>()]);
        var viewModel = new TestContentPageViewModel(page.Object, scheduler);
        viewModel.InitializeProperties();
        var content = viewModel.CreatedContent;
        Assert.IsNotNull(content);
        Assert.AreEqual(0, viewModel.Content.Count);

        await viewModel.CleanupAsync().WaitAsync(TestTimeout);
        scheduler.Drain();
        await content.Cleaned.Task.WaitAsync(TestTimeout);

        Assert.AreEqual(0, viewModel.Content.Count);
        page.VerifyRemove(p => p.ItemsChanged -= It.IsAny<TypedEventHandler<object, IItemsChangedEventArgs>>(), Times.Once);
    }

    [TestMethod]
    public async Task ParametersCleanupReleasesItsOwnedListSubscriptions()
    {
        var list = CreateListPage();
        var page = new Mock<IParametersPage>();
        page.SetupGet(p => p.Command).Returns(new ListItem(new NoOpCommand()));
        page.SetupGet(p => p.Parameters).Returns([new CommandParameterRun { Command = list.Object }]);
        var viewModel = new ParametersPageViewModel(
            page.Object, TaskScheduler.Default, new TestHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance);
        viewModel.InitializeProperties();
        var ownedList = ((CommandParameterRunViewModel)viewModel.Items[0]).ListViewModel;
        Assert.IsNotNull(ownedList);

        await viewModel.CleanupAsync().WaitAsync(TestTimeout);
        Assert.IsTrue(ownedList.IsDiscarded);
        await ownedList.CleanupAsync().WaitAsync(TestTimeout);

        Assert.AreEqual(0, viewModel.Items.Count);
        list.VerifyRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
        list.VerifyRemove(p => p.ItemsChanged -= It.IsAny<TypedEventHandler<object, IItemsChangedEventArgs>>(), Times.Once);
    }

    [TestMethod]
    public async Task RetainedParametersPageSuppressesCommandsUntilItReturns()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new Mock<IParametersPage>();
        page.SetupGet(p => p.Command).Returns(new ListItem(new NoOpCommand()));
        page.SetupGet(p => p.Parameters).Returns([]);
        var viewModel = new ParametersPageViewModel(
            page.Object, scheduler, new TestHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance);
        var recipient = new object();
        var updates = 0;
        WeakReferenceMessenger.Default.Register<UpdateCommandBarMessage>(recipient, (_, message) =>
        {
            if (ReferenceEquals(message.ViewModel, viewModel.Command))
            {
                updates++;
            }
        });

        try
        {
            await viewModel.InitializeAsync();
            viewModel.SuspendForNavigation();
            scheduler.Drain();
            Assert.IsTrue(viewModel.IsInitialized);
            Assert.AreEqual(0, updates);
            Assert.IsFalse(viewModel.IsDiscarded);

            await viewModel.ResumeAfterNavigation();
            scheduler.Drain();
            Assert.AreEqual(1, updates);
            page.VerifyRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Never);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            await viewModel.CleanupAsync().WaitAsync(TestTimeout);
        }
    }

    private static Mock<IListPage> CreateListPage()
    {
        var page = new Mock<IListPage>();
        page.Setup(p => p.GetItems()).Returns([]);
        return page;
    }

    private static ListViewModel CreateListViewModel(IListPage page) =>
        new(page, TaskScheduler.Default, new TestHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance);

    private static void Block(TaskCompletionSource entered, ManualResetEventSlim release)
    {
        entered.TrySetResult();
        Assert.IsTrue(release.Wait(TestTimeout * 2), "The extension call was not released.");
    }

    private sealed partial class TestHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Page cleanup test host";
    }

    private sealed partial class TestContentPageViewModel(IContentPage page, TaskScheduler scheduler)
        : ContentPageViewModel(page, scheduler, new TestHost(), CommandProviderContext.Empty)
    {
        internal TestContentViewModel? CreatedContent { get; private set; }

        public override ContentViewModel? ViewModelFromContent(IContent content, WeakReference<IPageContext> context) =>
            CreatedContent = new(context);
    }

    private sealed partial class TestContentViewModel(WeakReference<IPageContext> context) : ContentViewModel(context)
    {
        internal TaskCompletionSource Cleaned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void InitializeProperties()
        {
        }

        protected override void UnsafeCleanup() => Cleaned.SetResult();
    }

    private sealed class QueuedTaskScheduler : TaskScheduler
    {
        private readonly ConcurrentQueue<Task> _tasks = new();

        protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();

        protected override void QueueTask(Task task) => _tasks.Enqueue(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        internal void Drain()
        {
            while (_tasks.TryDequeue(out var task))
            {
                TryExecuteTask(task);
                task.GetAwaiter().GetResult();
            }
        }
    }
}
