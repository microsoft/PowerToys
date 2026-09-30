// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
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

public sealed partial class PageCleanupTests
{
    private static readonly string[] ExpectedSubscriptionEvents = ["add", "remove"];

    [TestMethod]
    public async Task ConcurrentCleanupCallsShareOneTaskAndRunTeardownOnce()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new Mock<IPage>();
        var viewModel = new BlockingCleanupPageViewModel(page.Object, entered, release);
        viewModel.InitializeProperties();
        Task? firstCleanup = null;
        Task? secondCleanup = null;
        var secondCall = Task.CompletedTask;
        var firstCall = Task.Run(() =>
        {
            firstCleanup = viewModel.CleanupAsync();
        });

        try
        {
            await entered.Task.WaitAsync(TestTimeout);
            secondCall = Task.Run(() =>
            {
                secondCleanup = viewModel.CleanupAsync();
            });
            await secondCall.WaitAsync(TestTimeout);

            Assert.IsTrue(viewModel.IsDiscarded);
            Assert.IsFalse(firstCall.IsCompleted);
            Assert.IsNotNull(secondCleanup);
            Assert.IsFalse(secondCleanup.IsCompleted);
            page.VerifyRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Never);
        }
        finally
        {
            release.Set();
            await Task.WhenAll(firstCall, secondCall).WaitAsync(TestTimeout);
            await Task.WhenAll(firstCleanup ?? viewModel.CleanupAsync(), secondCleanup ?? Task.CompletedTask).WaitAsync(TestTimeout);
        }

        Assert.AreSame(firstCleanup, secondCleanup);
        Assert.AreSame(firstCleanup, viewModel.CleanupAsync());
        Assert.AreEqual(1, viewModel.CleanupRequestCount);
        page.VerifyRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task CleanupWaitsForEveryInFlightNotification(bool finishTitleFirst)
    {
        using var releaseTitle = new ManualResetEventSlim();
        using var releaseLoading = new ManualResetEventSlim();
        var titleEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loadingEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new Mock<IPage>();
        TypedEventHandler<object, IPropChangedEventArgs>? notification = null;
        page.SetupAdd(p => p.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback<TypedEventHandler<object, IPropChangedEventArgs>>(handler => notification = handler);
        var viewModel = new PageViewModel(page.Object, new QueuedTaskScheduler(), new TestHost(), CommandProviderContext.Empty);
        viewModel.InitializeProperties();
        Assert.IsNotNull(notification);

        page.SetupGet(p => p.Title).Returns(() =>
        {
            Block(titleEntered, releaseTitle);
            return "Updated title";
        });
        page.SetupGet(p => p.IsLoading).Returns(() =>
        {
            Block(loadingEntered, releaseLoading);
            return false;
        });

        var titleUpdate = Task.Run(() => notification(page.Object, new PropChangedEventArgs(nameof(IPage.Title))));
        var loadingUpdate = Task.Run(() => notification(page.Object, new PropChangedEventArgs(nameof(IPage.IsLoading))));
        Task? cleanup = null;
        try
        {
            await Task.WhenAll(titleEntered.Task, loadingEntered.Task).WaitAsync(TestTimeout);
            cleanup = viewModel.CleanupAsync();
            Assert.IsTrue(viewModel.IsDiscarded);
            Assert.IsFalse(cleanup.IsCompleted);

            await Task.Run(() => notification(page.Object, new PropChangedEventArgs(nameof(IPage.Title)))).WaitAsync(TestTimeout);
            if (finishTitleFirst)
            {
                releaseTitle.Set();
                await titleUpdate.WaitAsync(TestTimeout);
            }
            else
            {
                releaseLoading.Set();
                await loadingUpdate.WaitAsync(TestTimeout);
            }

            Assert.IsFalse(cleanup.IsCompleted, "The other extension call still owns a page operation.");
            page.VerifyRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Never);
        }
        finally
        {
            releaseTitle.Set();
            releaseLoading.Set();
            await Task.WhenAll(titleUpdate, loadingUpdate).WaitAsync(TestTimeout);
            await (cleanup ?? viewModel.CleanupAsync()).WaitAsync(TestTimeout);
        }

        page.VerifyGet(p => p.Title, Times.Exactly(2));
        page.VerifyGet(p => p.IsLoading, Times.Exactly(2));
        page.VerifyRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task CleanupRequestedInsideAnExtensionGetterDoesNotDeadlock(bool duringInitialization)
    {
        var page = new Mock<IPage>();
        var subscriptionEvents = new ConcurrentQueue<string>();
        TypedEventHandler<object, IPropChangedEventArgs>? notification = null;
        page.SetupAdd(p => p.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback<TypedEventHandler<object, IPropChangedEventArgs>>(handler =>
            {
                notification = handler;
                subscriptionEvents.Enqueue("add");
            });
        page.SetupRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback(() => subscriptionEvents.Enqueue("remove"));
        var viewModel = new PageViewModel(page.Object, new QueuedTaskScheduler(), new TestHost(), CommandProviderContext.Empty);
        if (!duringInitialization)
        {
            viewModel.InitializeProperties();
        }

        Task? cleanup = null;
        var cleanupReturnedBeforeGetter = false;
        page.SetupGet(p => p.Title).Returns(() =>
        {
            cleanup = viewModel.CleanupAsync();
            cleanupReturnedBeforeGetter = !cleanup.IsCompleted;
            return "Reentrant title";
        });

        try
        {
            if (duringInitialization)
            {
                await Task.Run(viewModel.InitializeProperties).WaitAsync(TestTimeout);
            }
            else
            {
                Assert.IsNotNull(notification);
                await Task.Run(() => notification(page.Object, new PropChangedEventArgs(nameof(IPage.Title)))).WaitAsync(TestTimeout);
            }

            Assert.IsNotNull(cleanup);
            Assert.IsTrue(cleanupReturnedBeforeGetter);
            await cleanup.WaitAsync(TestTimeout);
            Assert.IsTrue(viewModel.IsDiscarded);
            CollectionAssert.AreEqual(ExpectedSubscriptionEvents, subscriptionEvents.ToArray());
        }
        finally
        {
            await (cleanup ?? viewModel.CleanupAsync()).WaitAsync(TestTimeout);
        }
    }

    [TestMethod]
    public async Task ParametersCreatedAfterCleanupWasRequestedReleaseTheirListSubscriptions()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var childUnsubscribed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var list = CreateListPage();
        list.SetupRemove(p => p.ItemsChanged -= It.IsAny<TypedEventHandler<object, IItemsChangedEventArgs>>())
            .Callback(() => childUnsubscribed.TrySetResult());
        var page = new Mock<IParametersPage>();
        page.SetupGet(p => p.Command).Returns(new ListItem(new NoOpCommand()));
        page.SetupGet(p => p.Parameters).Returns(() =>
        {
            Block(entered, release);
            return [new CommandParameterRun { Command = list.Object }];
        });
        var viewModel = new ParametersPageViewModel(
            page.Object, new QueuedTaskScheduler(), new TestHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance);
        var initialization = Task.Run(viewModel.InitializeProperties);
        Task? cleanup = null;
        try
        {
            await entered.Task.WaitAsync(TestTimeout);
            cleanup = viewModel.CleanupAsync();
            Assert.IsTrue(viewModel.IsDiscarded);
            Assert.IsFalse(cleanup.IsCompleted);
            Assert.AreEqual(0, viewModel.Items.Count);
            list.VerifyAdd(p => p.ItemsChanged += It.IsAny<TypedEventHandler<object, IItemsChangedEventArgs>>(), Times.Never);
        }
        finally
        {
            release.Set();
            await initialization.WaitAsync(TestTimeout);
            await (cleanup ?? viewModel.CleanupAsync()).WaitAsync(TestTimeout);
        }

        await childUnsubscribed.Task.WaitAsync(TestTimeout);
        Assert.AreEqual(0, viewModel.Items.Count);
        list.VerifyAdd(p => p.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
        list.VerifyAdd(p => p.ItemsChanged += It.IsAny<TypedEventHandler<object, IItemsChangedEventArgs>>(), Times.Once);
        list.VerifyRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
        list.VerifyRemove(p => p.ItemsChanged -= It.IsAny<TypedEventHandler<object, IItemsChangedEventArgs>>(), Times.Once);
    }

    [TestMethod]
    public async Task ParametersCleanupDoesNotWaitForABlockedChildUnsubscribe()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var list = CreateListPage();
        var page = new Mock<IParametersPage>();
        page.SetupGet(p => p.Command).Returns(new ListItem(new NoOpCommand()));
        page.SetupGet(p => p.Parameters).Returns([new CommandParameterRun { Command = list.Object }]);
        var viewModel = new ParametersPageViewModel(
            page.Object, new QueuedTaskScheduler(), new TestHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance);
        viewModel.InitializeProperties();
        var ownedList = ((CommandParameterRunViewModel)viewModel.Items[0]).ListViewModel;
        Assert.IsNotNull(ownedList);
        list.SetupRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback(() => Block(entered, release));

        var cleanup = viewModel.CleanupAsync();
        try
        {
            await entered.Task.WaitAsync(TestTimeout);
            Assert.IsTrue(ownedList.IsDiscarded);
            Assert.IsFalse(ownedList.CleanupAsync().IsCompleted);
            await cleanup.WaitAsync(TestTimeout);
            Assert.AreEqual(0, viewModel.Items.Count);
            list.VerifyRemove(p => p.ItemsChanged -= It.IsAny<TypedEventHandler<object, IItemsChangedEventArgs>>(), Times.Never);
        }
        finally
        {
            release.Set();
            await cleanup.WaitAsync(TestTimeout);
            await ownedList.CleanupAsync().WaitAsync(TestTimeout);
        }

        list.VerifyRemove(p => p.ItemsChanged -= It.IsAny<TypedEventHandler<object, IItemsChangedEventArgs>>(), Times.Once);
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public async Task CompletedParameterClearsItsListAndOnlyFocusesWhenActive(bool suspendBeforeCompletion, bool suspendBeforeDispatch)
    {
        var scheduler = new QueuedTaskScheduler();
        var list = CreateListPage();
        var parameter = new CommandParameterRun { Command = list.Object, Value = "Old value" };
        var page = new Mock<IParametersPage>();
        page.SetupGet(p => p.Command).Returns(new ListItem(new NoOpCommand()));
        page.SetupGet(p => p.Parameters).Returns([parameter, new StringParameterRun { Text = "Filled value" }]);
        var viewModel = new ParametersPageViewModel(
            page.Object, scheduler, new TestHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance);
        await viewModel.InitializeAsync();
        scheduler.Drain();
        var parameterViewModel = (CommandParameterRunViewModel)viewModel.Items[0];
        var nextParameter = viewModel.Items[1];
        var ownedList = parameterViewModel.ListViewModel;
        Assert.IsNotNull(ownedList);

        var recipient = new object();
        var focusUpdates = 0;
        var commandUpdates = 0;
        WeakReferenceMessenger.Default.Register<FocusParamMessage>(recipient, (_, message) =>
        {
            if (ReferenceEquals(message.Parameter, nextParameter))
            {
                focusUpdates++;
            }
        });
        WeakReferenceMessenger.Default.Register<UpdateCommandBarMessage>(recipient, (_, message) =>
        {
            if (ReferenceEquals(message.ViewModel, viewModel.Command))
            {
                commandUpdates++;
            }
        });

        try
        {
            parameterViewModel.BeginEditing();
            viewModel.SetActiveListParameter(parameterViewModel);
            scheduler.Drain();
            Assert.IsTrue(viewModel.HasActiveList);
            if (suspendBeforeCompletion)
            {
                viewModel.SuspendForNavigation();
            }

            parameter.Value = "New value";
            if (suspendBeforeDispatch)
            {
                viewModel.SuspendForNavigation();
            }

            Assert.IsTrue(viewModel.HasActiveList, "Picker state must be reconciled on the UI scheduler.");
            scheduler.Drain();
            Assert.IsFalse(parameterViewModel.NeedsValue);
            Assert.IsFalse(parameterViewModel.IsEditing);
            Assert.IsFalse(viewModel.HasActiveList, "A completed parameter must stop showing its picker while the page is retained.");
            Assert.IsNull(viewModel.ActiveListViewModel);
            var isSuspended = suspendBeforeCompletion || suspendBeforeDispatch;
            var expectedFocusUpdates = isSuspended ? 0 : 1;
            Assert.AreEqual(expectedFocusUpdates, focusUpdates);
            if (isSuspended)
            {
                Assert.AreEqual(0, commandUpdates);
            }

            var updatesBeforeResume = commandUpdates;
            await viewModel.ResumeAfterNavigation();
            scheduler.Drain();
            Assert.IsTrue(viewModel.ShowCommand);
            Assert.IsFalse(viewModel.HasActiveList, "The completed picker must not hide the ready command after returning.");
            Assert.AreEqual(expectedFocusUpdates, focusUpdates);
            Assert.AreEqual(updatesBeforeResume + 1, commandUpdates);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            await viewModel.CleanupAsync().WaitAsync(TestTimeout);
            await ownedList.CleanupAsync().WaitAsync(TestTimeout);
        }
    }

    [TestMethod]
    public async Task RetainedContentPageRestoresPresentationWithoutReloading()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new Mock<IContentPage>();
        page.Setup(p => p.GetContent()).Returns([]);
        page.SetupGet(p => p.Details).Returns(Mock.Of<IDetails>());
        var viewModel = new ContentPageViewModel(page.Object, scheduler, new TestHost(), CommandProviderContext.Empty);
        var recipient = new object();
        var commandUpdates = 0;
        var detailsUpdates = 0;
        WeakReferenceMessenger.Default.Register<UpdateCommandBarMessage>(recipient, (_, message) =>
        {
            if (ReferenceEquals(message.ViewModel, viewModel))
            {
                commandUpdates++;
            }
        });
        WeakReferenceMessenger.Default.Register<ShowDetailsMessage>(recipient, (_, message) =>
        {
            if (ReferenceEquals(message.Details, viewModel.Details))
            {
                detailsUpdates++;
            }
        });

        try
        {
            await viewModel.InitializeAsync();
            Assert.IsNotNull(viewModel.Details);
            for (var visit = 1; visit <= 2; visit++)
            {
                viewModel.SuspendForNavigation();
                scheduler.Drain();
                Assert.IsTrue(viewModel.IsInitialized);
                Assert.IsFalse(viewModel.IsDiscarded);
                Assert.AreEqual(visit - 1, commandUpdates);
                Assert.AreEqual(visit - 1, detailsUpdates);

                await viewModel.ResumeAfterNavigation();
                scheduler.Drain();
                Assert.AreEqual(visit, commandUpdates);
                Assert.AreEqual(visit, detailsUpdates);
            }

            page.Verify(p => p.GetContent(), Times.Once);
            page.VerifyAdd(p => p.ItemsChanged += It.IsAny<TypedEventHandler<object, IItemsChangedEventArgs>>(), Times.Once);
            page.VerifyRemove(p => p.ItemsChanged -= It.IsAny<TypedEventHandler<object, IItemsChangedEventArgs>>(), Times.Never);
            page.VerifyRemove(p => p.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Never);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            await viewModel.CleanupAsync().WaitAsync(TestTimeout);
        }
    }

    private sealed partial class BlockingCleanupPageViewModel(IPage page, TaskCompletionSource entered, ManualResetEventSlim release)
        : PageViewModel(page, new QueuedTaskScheduler(), new TestHost(), CommandProviderContext.Empty)
    {
        private int _cleanupRequestCount;

        internal int CleanupRequestCount => Volatile.Read(ref _cleanupRequestCount);

        protected override void OnCleanupRequested()
        {
            Interlocked.Increment(ref _cleanupRequestCount);
            Block(entered, release);
        }
    }
}
