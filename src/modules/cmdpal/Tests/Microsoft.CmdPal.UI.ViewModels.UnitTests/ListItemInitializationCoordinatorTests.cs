// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class ListItemInitializationCoordinatorTests
{
    private static readonly int[] RealizedPriorityOrder = [0, 3, 1, 2];
    private static readonly int[] SequentialOrder = [0, 1, 2, 3];
    private static readonly int[] EarlyRealizedPriorityOrder = [3, 0, 1, 2];
    private static readonly int[] SharedDemandPriorityOrder = [0, 3, 2, 1];
    private static readonly int[] StoppedFallbackOrder = [0, 3];
    private static readonly int[] RemovedPendingOrder = [0, 1, 2];
    private static readonly int[] BatchedPriorityOrder = [3, 1, 2, 0];
    private static readonly TestPageContext TestContext = new();

    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null) =>
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
    }

    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Initialization test host";
    }

    private sealed partial class RefreshablePage(IListItem[] items) : ListPage
    {
        public override IListItem[] GetItems() => items;

        internal void Refresh() => RaiseItemsChanged(items.Length);
    }

    private sealed partial class ThrowingCleanupContextItem : CommandContextItemViewModel
    {
        internal ThrowingCleanupContextItem()
            : base(new CommandContextItem(new NoOpCommand()), new(TestContext))
        {
        }

        internal int CleanupCount { get; private set; }

        public override void SafeCleanup()
        {
            CleanupCount++;
            throw new InvalidOperationException("Expected cleanup failure");
        }
    }

    private sealed partial class CleanupFailureListItemViewModel : ListItemViewModel
    {
        internal CleanupFailureListItemViewModel(IListItem model, CommandContextItemViewModel cleanupItem)
            : base(model, new(TestContext), DefaultContextMenuFactory.Instance)
        {
            UnsafeMoreCommands.Add(cleanupItem);
        }
    }

    private sealed partial class TrackingListItem : ListItem
    {
        private readonly int _index;
        private readonly ConcurrentQueue<int> _initializationOrder;
        private readonly ManualResetEventSlim? _initializationStarted;
        private readonly ManualResetEventSlim? _continueInitialization;
        private int _initializationCount;

        public TrackingListItem(
            int index,
            ConcurrentQueue<int> initializationOrder,
            ManualResetEventSlim? initializationStarted = null,
            ManualResetEventSlim? continueInitialization = null)
            : base(new NoOpCommand { Name = $"Item {index}" })
        {
            _index = index;
            _initializationOrder = initializationOrder;
            _initializationStarted = initializationStarted;
            _continueInitialization = continueInitialization;
            TextToSuggest = $"item-{index}";
        }

        public int InitializationCount => Volatile.Read(ref _initializationCount);

        public Action? OnInitializing { get; set; }

        public override ITag[] Tags
        {
            get
            {
                Interlocked.Increment(ref _initializationCount);
                _initializationOrder.Enqueue(_index);
                OnInitializing?.Invoke();
                _initializationStarted?.Set();

                if (_continueInitialization is not null && !_continueInitialization.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Test initialization was not released.");
                }

                return [];
            }

            set
            {
            }
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task RealizedItemJumpsAheadOfSpeculativeInitialization()
    {
        using var firstStarted = new ManualResetEventSlim();
        using var continueFirst = new ManualResetEventSlim();
        var order = new ConcurrentQueue<int>();
        var (models, viewModels) = CreateItems(4, order, firstStarted, continueFirst);
        var coordinator = new ListItemInitializationCoordinator(viewModels);
        var worker = Task.Run(() => coordinator.Run(CancellationToken.None));

        try
        {
            Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(2)));
            var registration = viewModels[3].BeginRealization();
            Assert.IsTrue(registration.IsValid);

            continueFirst.Set();
            await worker.WaitAsync(TimeSpan.FromSeconds(2));
            registration.Release();

            CollectionAssert.AreEqual(RealizedPriorityOrder, order.ToArray());
            Assert.AreEqual(1, models[3].InitializationCount);
        }
        finally
        {
            continueFirst.Set();
            coordinator.Stop();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task UnrealizedItemLosesPriorityBeforeWorkerDequeuesIt()
    {
        using var firstStarted = new ManualResetEventSlim();
        using var continueFirst = new ManualResetEventSlim();
        var order = new ConcurrentQueue<int>();
        var (_, viewModels) = CreateItems(4, order, firstStarted, continueFirst);
        var coordinator = new ListItemInitializationCoordinator(viewModels);
        var worker = Task.Run(() => coordinator.Run(CancellationToken.None));

        try
        {
            Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(2)));
            var registration = viewModels[3].BeginRealization();
            Assert.IsTrue(registration.IsValid);
            registration.Release();

            continueFirst.Set();
            await worker.WaitAsync(TimeSpan.FromSeconds(2));

            CollectionAssert.AreEqual(SequentialOrder, order.ToArray());
        }
        finally
        {
            continueFirst.Set();
            coordinator.Stop();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task SelectionRequestUsesSameWorkerAndJumpsAhead()
    {
        using var firstStarted = new ManualResetEventSlim();
        using var continueFirst = new ManualResetEventSlim();
        var order = new ConcurrentQueue<int>();
        var (_, viewModels) = CreateItems(4, order, firstStarted, continueFirst);
        var coordinator = new ListItemInitializationCoordinator(viewModels);
        var worker = Task.Run(() => coordinator.Run(CancellationToken.None));

        try
        {
            Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(2)));
            var selectedInitialization = viewModels[3].RequestInitializationAsync(CancellationToken.None);

            continueFirst.Set();
            Assert.IsTrue(await selectedInitialization.WaitAsync(TimeSpan.FromSeconds(2)));
            await worker.WaitAsync(TimeSpan.FromSeconds(2));

            CollectionAssert.AreEqual(RealizedPriorityOrder, order.ToArray());
        }
        finally
        {
            continueFirst.Set();
            coordinator.Stop();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task ConcurrentInitializationClaimsRunExtensionGetterOnce()
    {
        using var initializationStarted = new ManualResetEventSlim();
        using var continueInitialization = new ManualResetEventSlim();
        var order = new ConcurrentQueue<int>();
        var (models, viewModels) = CreateItems(1, order, initializationStarted, continueInitialization);
        var viewModel = viewModels[0];

        var first = Task.Run(viewModel.InitializePropertiesOnce);
        Assert.IsTrue(initializationStarted.Wait(TimeSpan.FromSeconds(2)));

        var waiter = viewModel.WaitForInitializationAsync(CancellationToken.None);
        var second = Task.Run(viewModel.InitializePropertiesOnce);
        await second.WaitAsync(TimeSpan.FromSeconds(2));

        continueInitialization.Set();
        await first.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.IsTrue(await waiter.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.AreEqual(1, models[0].InitializationCount);
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task SelectionAfterCoordinatorStopsUsesBackgroundFallback()
    {
        var order = new ConcurrentQueue<int>();
        var (_, viewModels) = CreateItems(1, order);
        var coordinator = new ListItemInitializationCoordinator(viewModels);
        coordinator.Stop();

        var initialized = await viewModels[0].RequestInitializationAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.IsTrue(initialized);
        Assert.AreEqual(1, order.Count);
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task SelectionUsesCurrentCoordinatorAndOldCoordinatorCannotInitializeIt()
    {
        var order = new ConcurrentQueue<int>();
        var (_, viewModels) = CreateItems(1, order);
        var oldCoordinator = new ListItemInitializationCoordinator(viewModels);
        var currentCoordinator = new ListItemInitializationCoordinator(viewModels);

        var selectedInitialization = viewModels[0].RequestInitializationAsync(CancellationToken.None);
        oldCoordinator.Run(CancellationToken.None);
        Assert.AreEqual(0, order.Count);
        Assert.IsFalse(selectedInitialization.IsCompleted);

        currentCoordinator.Run(CancellationToken.None);
        Assert.IsTrue(await selectedInitialization.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.AreEqual(1, order.Count);
    }

    [TestMethod]
    [Timeout(15000)]
    public void RealizationBeforeCoordinatorCreationRetainsPriority()
    {
        var order = new ConcurrentQueue<int>();
        var (_, viewModels) = CreateItems(4, order);
        var registration = viewModels[3].BeginRealization();
        Assert.IsTrue(registration.IsValid);

        var coordinator = new ListItemInitializationCoordinator(viewModels);
        coordinator.Run(CancellationToken.None);
        registration.Release();

        CollectionAssert.AreEqual(EarlyRealizedPriorityOrder, order.ToArray());
    }

    [TestMethod]
    [Timeout(15000)]
    public void UnchangedRealizationRemainsDemandedAfterCoordinatorReplacement()
    {
        var order = new ConcurrentQueue<int>();
        var (_, viewModels) = CreateItems(4, order);
        var oldCoordinator = new ListItemInitializationCoordinator(viewModels);
        var registration = viewModels[3].BeginRealization();
        oldCoordinator.Stop();

        var currentCoordinator = new ListItemInitializationCoordinator(viewModels);
        Assert.IsTrue(registration.IsFor(viewModels[3]));
        currentCoordinator.Run(CancellationToken.None);
        registration.Release();

        CollectionAssert.AreEqual(EarlyRealizedPriorityOrder, order.ToArray());
    }

    [TestMethod]
    [Timeout(15000)]
    public void ReleasingOldRegistrationCannotRemoveNewDemandAfterReplacement()
    {
        var order = new ConcurrentQueue<int>();
        var (models, viewModels) = CreateItems(4, order);
        var oldCoordinator = new ListItemInitializationCoordinator(viewModels);
        var oldRegistration = viewModels[3].BeginRealization();
        oldCoordinator.Stop();

        var currentCoordinator = new ListItemInitializationCoordinator(viewModels);
        var currentRegistration = viewModels[3].BeginRealization();
        oldRegistration.Release();
        oldRegistration.Release();
        Assert.IsFalse(oldRegistration.IsValid);
        Assert.IsTrue(currentRegistration.IsFor(viewModels[3]));

        currentCoordinator.Run(CancellationToken.None);
        currentRegistration.Release();
        CollectionAssert.AreEqual(EarlyRealizedPriorityOrder, order.ToArray());
        Assert.AreEqual(1, models[3].InitializationCount);
    }

    [TestMethod]
    [Timeout(15000)]
    public void ReplacementPrunesReleasedNodesAndPreservesLiveDemand()
    {
        var order = new ConcurrentQueue<int>();
        var (models, viewModels) = CreateItems(4, order);
        var previous = new ListItemInitializationCoordinator(viewModels);
        var item = viewModels[3];
        var first = item.BeginRealization();
        var firstNode = GetRetainedDemandNodes(item)[0];
        AddReleasedRealizations(item, 64);
        var second = item.BeginRealization();
        var secondNode = GetRetainedDemandNodes(item)[0];
        AddReleasedRealizations(item, 64);
        var originalNodes = GetRetainedDemandNodes(item);
        ListItemInitializationCoordinator? replacement = null;

        try
        {
            Assert.AreEqual(130, originalNodes.Length);
            replacement = new ListItemInitializationCoordinator(viewModels);
            previous.Stop();

            // The inactive head is intentionally retained; live interior nodes are
            // kept in place rather than copied or dropped with the released nodes.
            CollectionAssert.AreEqual(
                new[] { originalNodes[0], secondNode, firstNode },
                GetRetainedDemandNodes(item));
            Assert.IsFalse(originalNodes[0].Demand.IsActive);
            Assert.IsTrue(first.IsFor(item));
            Assert.IsTrue(second.IsFor(item));

            replacement.Run(CancellationToken.None);
            CollectionAssert.AreEqual(EarlyRealizedPriorityOrder, order.ToArray());
            Assert.AreEqual(1, models[3].InitializationCount);
            Assert.AreEqual(0, GetRetainedDemandNodes(item).Length);
        }
        finally
        {
            first.Release();
            second.Release();
            previous.Stop();
            replacement?.Stop();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public void RepeatedReplacementBoundsRetainedReleasedNodes()
    {
        var order = new ConcurrentQueue<int>();
        var (_, viewModels) = CreateItems(4, order);
        var coordinator = new ListItemInitializationCoordinator(viewModels);
        var item = viewModels[3];
        var visible = item.BeginRealization();
        var liveNode = GetRetainedDemandNodes(item)[0];

        try
        {
            for (var iteration = 0; iteration < 32; iteration++)
            {
                AddReleasedRealizations(item, 32);
                var previous = coordinator;
                coordinator = new ListItemInitializationCoordinator(viewModels);
                previous.Stop();

                var retained = GetRetainedDemandNodes(item);
                Assert.AreEqual(2, retained.Length, "Each replay should retain only live demand and its captured head.");
                Assert.IsFalse(retained[0].Demand.IsActive);
                Assert.AreSame(liveNode, retained[1]);
            }

            coordinator.Run(CancellationToken.None);
            CollectionAssert.AreEqual(EarlyRealizedPriorityOrder, order.ToArray());
        }
        finally
        {
            visible.Release();
            coordinator.Stop();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task ReplacementPrunesCanceledSelectionWithoutWithdrawingRealization()
    {
        using var cancellation = new CancellationTokenSource();
        var order = new ConcurrentQueue<int>();
        var (_, viewModels) = CreateItems(4, order);
        var previous = new ListItemInitializationCoordinator(viewModels);
        var item = viewModels[3];
        var selection = item.RequestInitializationAsync(cancellation.Token);
        var visible = item.BeginRealization();
        var liveNode = GetRetainedDemandNodes(item)[0];
        AddReleasedRealizations(item, 32);
        var retainedHead = GetRetainedDemandNodes(item)[0];
        ListItemInitializationCoordinator? replacement = null;

        try
        {
            cancellation.Cancel();
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await selection);
            replacement = new ListItemInitializationCoordinator(viewModels);
            previous.Stop();

            CollectionAssert.AreEqual(new[] { retainedHead, liveNode }, GetRetainedDemandNodes(item));
            Assert.IsTrue(visible.IsFor(item));
            replacement.Run(CancellationToken.None);
            CollectionAssert.AreEqual(EarlyRealizedPriorityOrder, order.ToArray());
        }
        finally
        {
            visible.Release();
            previous.Stop();
            replacement?.Stop();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task CanceledSelectionLosesPriorityBeforeDequeue()
    {
        using var firstStarted = new ManualResetEventSlim();
        using var continueFirst = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var order = new ConcurrentQueue<int>();
        var (_, viewModels) = CreateItems(4, order, firstStarted, continueFirst);
        var coordinator = new ListItemInitializationCoordinator(viewModels);
        var worker = Task.Run(() => coordinator.Run(CancellationToken.None));
        try
        {
            Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(2)));
            var selected = viewModels[1].RequestInitializationAsync(cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await selected);
            var visible = viewModels[3].BeginRealization();

            continueFirst.Set();
            await worker.WaitAsync(TimeSpan.FromSeconds(2));
            visible.Release();
            CollectionAssert.AreEqual(RealizedPriorityOrder, order.ToArray());
        }
        finally
        {
            continueFirst.Set();
            coordinator.Stop();
            await worker.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task CancelingSelectionDoesNotWithdrawRealization()
    {
        using var firstStarted = new ManualResetEventSlim();
        using var continueFirst = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var order = new ConcurrentQueue<int>();
        var (_, viewModels) = CreateItems(4, order, firstStarted, continueFirst);
        var coordinator = new ListItemInitializationCoordinator(viewModels);
        var worker = Task.Run(() => coordinator.Run(CancellationToken.None));
        try
        {
            Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(2)));
            var selected = viewModels[3].RequestInitializationAsync(cancellation.Token);
            var visible = viewModels[3].BeginRealization();
            var laterVisible = viewModels[2].BeginRealization();
            cancellation.Cancel();
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await selected);

            continueFirst.Set();
            await worker.WaitAsync(TimeSpan.FromSeconds(2));
            visible.Release();
            laterVisible.Release();
            CollectionAssert.AreEqual(SharedDemandPriorityOrder, order.ToArray());
        }
        finally
        {
            continueFirst.Set();
            coordinator.Stop();
            await worker.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task ReleasingRealizationDoesNotWithdrawSelection()
    {
        using var firstStarted = new ManualResetEventSlim();
        using var continueFirst = new ManualResetEventSlim();
        var order = new ConcurrentQueue<int>();
        var (_, viewModels) = CreateItems(4, order, firstStarted, continueFirst);
        var coordinator = new ListItemInitializationCoordinator(viewModels);
        var worker = Task.Run(() => coordinator.Run(CancellationToken.None));
        try
        {
            Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(2)));
            var visible = viewModels[3].BeginRealization();
            var selected = viewModels[3].RequestInitializationAsync(CancellationToken.None);
            var laterVisible = viewModels[2].BeginRealization();
            visible.Release();

            continueFirst.Set();
            Assert.IsTrue(await selected.WaitAsync(TimeSpan.FromSeconds(2)));
            await worker.WaitAsync(TimeSpan.FromSeconds(2));
            laterVisible.Release();
            CollectionAssert.AreEqual(SharedDemandPriorityOrder, order.ToArray());
        }
        finally
        {
            continueFirst.Set();
            coordinator.Stop();
            await worker.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task SelectionSurvivesReplacementWhilePreviousWorkerIsStillRunning()
    {
        using var firstStarted = new ManualResetEventSlim();
        using var continueFirst = new ManualResetEventSlim();
        var order = new ConcurrentQueue<int>();
        var (models, viewModels) = CreateItems(4, order, firstStarted, continueFirst);
        var previous = new ListItemInitializationCoordinator(viewModels);
        var previousWorker = Task.Run(() => previous.Run(CancellationToken.None));
        ListItemInitializationCoordinator? replacement = null;
        Task? replacementWorker = null;
        try
        {
            Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(2)));
            var selected = viewModels[3].RequestInitializationAsync(CancellationToken.None);
            replacement = new ListItemInitializationCoordinator(viewModels);
            previous.Stop();
            replacementWorker = Task.Run(() => replacement.Run(CancellationToken.None));

            Assert.IsTrue(await selected.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.IsTrue(viewModels[3].SafeSlowInit());
            Assert.AreEqual("item-3", viewModels[3].TextToSuggest);
            Assert.IsFalse(previous.Completion.IsCompleted);
            Assert.AreEqual(1, models[3].InitializationCount);
        }
        finally
        {
            continueFirst.Set();
            previous.Stop();
            replacement?.Stop();
            await previousWorker.WaitAsync(TimeSpan.FromSeconds(2));
            if (replacementWorker is not null)
            {
                await replacementWorker.WaitAsync(TimeSpan.FromSeconds(2));
            }
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task CancellationBeforeReplacementDoesNotReplaySelectionPriority()
    {
        using var firstStarted = new ManualResetEventSlim();
        using var continueFirst = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var order = new ConcurrentQueue<int>();
        var (_, viewModels) = CreateItems(4, order, firstStarted, continueFirst);
        var previous = new ListItemInitializationCoordinator(viewModels);
        var previousWorker = Task.Run(() => previous.Run(CancellationToken.None));
        try
        {
            Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(2)));
            var selected = viewModels[1].RequestInitializationAsync(cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await selected);
            var visible = viewModels[3].BeginRealization();

            var replacement = new ListItemInitializationCoordinator(viewModels);
            previous.Stop();
            replacement.Run(CancellationToken.None);
            visible.Release();
            CollectionAssert.AreEqual(RealizedPriorityOrder, order.ToArray());
        }
        finally
        {
            continueFirst.Set();
            previous.Stop();
            await previousWorker.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task SelectionWithoutCoordinatorWaitsForAnExistingInitializationClaim()
    {
        using var initializationStarted = new ManualResetEventSlim();
        using var continueInitialization = new ManualResetEventSlim();
        var order = new ConcurrentQueue<int>();
        var (models, viewModels) = CreateItems(1, order, initializationStarted, continueInitialization);
        var worker = Task.Run(viewModels[0].InitializePropertiesOnce);
        try
        {
            Assert.IsTrue(initializationStarted.Wait(TimeSpan.FromSeconds(2)));
            var selected = viewModels[0].RequestInitializationAsync(CancellationToken.None);
            Assert.IsFalse(selected.IsCompleted);

            continueInitialization.Set();
            Assert.IsTrue(await selected.WaitAsync(TimeSpan.FromSeconds(2)));
            await worker.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.AreEqual(1, models[0].InitializationCount);
        }
        finally
        {
            continueInitialization.Set();
            await worker.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task SelectionFallbackWaitsForStoppedWorkerToReturn()
    {
        using var firstStarted = new ManualResetEventSlim();
        using var continueFirst = new ManualResetEventSlim();
        var order = new ConcurrentQueue<int>();
        var (models, viewModels) = CreateItems(4, order, firstStarted, continueFirst);
        var coordinator = new ListItemInitializationCoordinator(viewModels);
        var worker = Task.Run(() => coordinator.Run(CancellationToken.None));
        try
        {
            Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(2)));
            coordinator.Stop();
            Assert.IsFalse(coordinator.Completion.IsCompleted);
            var selected = viewModels[3].RequestInitializationAsync(CancellationToken.None);
            Assert.IsFalse(selected.IsCompleted);
            Assert.AreEqual(0, models[3].InitializationCount);

            continueFirst.Set();
            Assert.IsTrue(await selected.WaitAsync(TimeSpan.FromSeconds(2)));
            await worker.WaitAsync(TimeSpan.FromSeconds(2));
            CollectionAssert.AreEqual(StoppedFallbackOrder, order.ToArray());
        }
        finally
        {
            continueFirst.Set();
            coordinator.Stop();
            await worker.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task RemovingPendingItemSettlesItsWaiterWithoutInitializingIt()
    {
        var order = new ConcurrentQueue<int>();
        var (models, viewModels) = CreateItems(4, order);
        var coordinator = new ListItemInitializationCoordinator(viewModels);
        var selected = viewModels[3].RequestInitializationAsync(CancellationToken.None);
        viewModels[3].SafeCleanup();

        Assert.IsFalse(await selected.WaitAsync(TimeSpan.FromSeconds(2)));
        coordinator.Run(CancellationToken.None);
        Assert.AreEqual(0, models[3].InitializationCount);
        CollectionAssert.AreEqual(RemovedPendingOrder, order.ToArray());
    }

    [TestMethod]
    [Timeout(15000)]
    public void NewPriorityBatchDoesNotOvertakeAlreadyPublishedRequests()
    {
        var order = new ConcurrentQueue<int>();
        var (models, viewModels) = CreateItems(4, order);
        var coordinator = new ListItemInitializationCoordinator(viewModels);
        var first = viewModels[3].BeginRealization();
        var second = viewModels[1].BeginRealization();
        ListItemRealizationRegistration nextBatch = default;
        models[3].OnInitializing = () => nextBatch = viewModels[2].BeginRealization();

        coordinator.Run(CancellationToken.None);
        first.Release();
        second.Release();
        nextBatch.Release();
        CollectionAssert.AreEqual(BatchedPriorityOrder, order.ToArray());
    }

    [TestMethod]
    [Timeout(15000)]
    public void FailedItemDoesNotStopInitializationOfRemainingItems()
    {
        var order = new ConcurrentQueue<int>();
        var (models, viewModels) = CreateItems(4, order);
        models[1].OnInitializing = () => throw new InvalidOperationException("Expected extension failure");
        var coordinator = new ListItemInitializationCoordinator(viewModels);
        coordinator.Run(CancellationToken.None);

        CollectionAssert.AreEqual(SequentialOrder, order.ToArray());
        Assert.IsFalse(viewModels[1].InitializationWasSuccessful);
        Assert.IsTrue(viewModels[2].InitializationWasSuccessful);
        Assert.IsTrue(viewModels[3].InitializationWasSuccessful);
    }

    [TestMethod]
    [Timeout(15000)]
    public void EscapedErrorCleanupFailureDoesNotStopRemainingItems()
    {
        var order = new ConcurrentQueue<int>();
        var (models, viewModels) = CreateItems(4, order);
        var cleanupItem = new ThrowingCleanupContextItem();
        viewModels[1] = new CleanupFailureListItemViewModel(models[1], cleanupItem);
        Assert.IsTrue(viewModels[1].SafeFastInit());
        models[1].OnInitializing = () => throw new InvalidOperationException("Expected extension failure");

        var coordinator = new ListItemInitializationCoordinator(viewModels);
        coordinator.Run(CancellationToken.None);

        Assert.AreEqual(1, cleanupItem.CleanupCount);
        CollectionAssert.AreEqual(SequentialOrder, order.ToArray());
        Assert.IsTrue(viewModels[1].IsInitializationComplete);
        Assert.IsFalse(viewModels[1].InitializationWasSuccessful);
        Assert.IsTrue(viewModels[2].InitializationWasSuccessful);
        Assert.IsTrue(viewModels[3].InitializationWasSuccessful);
    }

    [TestMethod]
    [Timeout(15000)]
    public void CompletedItemDoesNotAllocateRealizationRegistrations()
    {
        var (_, viewModels) = CreateItems(1, new ConcurrentQueue<int>());
        var item = viewModels[0];
        item.InitializePropertiesOnce();
        Assert.IsFalse(item.BeginRealization().IsValid);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            item.BeginRealization();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(0L, allocated);
    }

    [TestMethod]
    [Timeout(30000)]
    public async Task ConcurrentRealizationAndCoordinatorReplacementRetainDemand()
    {
        for (var iteration = 0; iteration < 64; iteration++)
        {
            var order = new ConcurrentQueue<int>();
            var (_, viewModels) = CreateItems(16, order);
            var previous = new ListItemInitializationCoordinator(viewModels);
            using var start = new Barrier(2);
            var registrations = new ListItemRealizationRegistration[8];
            var producer = Task.Run(() =>
            {
                Assert.IsTrue(start.SignalAndWait(TimeSpan.FromSeconds(5)));
                for (var i = 0; i < registrations.Length; i++)
                {
                    registrations[i] = viewModels[(i * 2) + 1].BeginRealization();
                }
            });
            var replacing = Task.Run(() =>
            {
                Assert.IsTrue(start.SignalAndWait(TimeSpan.FromSeconds(5)));
                return new ListItemInitializationCoordinator(viewModels);
            });

            await Task.WhenAll(producer, replacing).WaitAsync(TimeSpan.FromSeconds(10));
            previous.Stop();
            var replacement = await replacing;
            replacement.Run(CancellationToken.None);
            var initialized = order.ToArray();
            Assert.AreEqual(16, initialized.Length);
            for (var i = 0; i < registrations.Length; i++)
            {
                Assert.AreEqual(1, initialized[i] % 2, "Every demanded item must precede speculative items.");
                registrations[i].Release();
            }
        }
    }

    [TestMethod]
    [Timeout(30000)]
    public async Task ConcurrentPublicationAndPruningPreserveDemandForAnotherReplacement()
    {
        for (var iteration = 0; iteration < 64; iteration++)
        {
            var order = new ConcurrentQueue<int>();
            var (_, viewModels) = CreateItems(4, order);
            var previous = new ListItemInitializationCoordinator(viewModels);
            var item = viewModels[3];
            var existing = item.BeginRealization();
            var existingNode = GetRetainedDemandNodes(item)[0];
            AddReleasedRealizations(item, 64);
            using var start = new Barrier(2);
            var publishing = Task.Run(() =>
            {
                Assert.IsTrue(start.SignalAndWait(TimeSpan.FromSeconds(5)));
                AddReleasedRealizations(item, 64);
                return item.BeginRealization();
            });
            var replacing = Task.Run(() =>
            {
                Assert.IsTrue(start.SignalAndWait(TimeSpan.FromSeconds(5)));
                return new ListItemInitializationCoordinator(viewModels);
            });
            ListItemInitializationCoordinator? finalCoordinator = null;

            try
            {
                await Task.WhenAll(publishing, replacing).WaitAsync(TimeSpan.FromSeconds(10));
                var arriving = await publishing;
                var replacement = await replacing;
                var arrivingNode = GetRetainedDemandNodes(item)[0];

                // Discard both earlier queues. Only replay from item-owned storage
                // can preserve the two live requests for this final coordinator.
                finalCoordinator = new ListItemInitializationCoordinator(viewModels);
                previous.Stop();
                replacement.Stop();
                CollectionAssert.AreEqual(new[] { arrivingNode, existingNode }, GetRetainedDemandNodes(item));
                Assert.IsTrue(existing.IsFor(item));
                Assert.IsTrue(arriving.IsFor(item));

                finalCoordinator.Run(CancellationToken.None);
                CollectionAssert.AreEqual(EarlyRealizedPriorityOrder, order.ToArray());
            }
            finally
            {
                existing.Release();
                previous.Stop();
                finalCoordinator?.Stop();
                if (publishing.IsCompletedSuccessfully)
                {
                    publishing.Result.Release();
                }

                if (replacing.IsCompletedSuccessfully)
                {
                    replacing.Result.Stop();
                }
            }
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(30000)]
    public async Task PruningCannotRestoreDemandAfterInitializationOrCleanup(bool cleanup)
    {
        for (var iteration = 0; iteration < 64; iteration++)
        {
            var (models, viewModels) = CreateItems(1, new ConcurrentQueue<int>());
            var previous = new ListItemInitializationCoordinator(viewModels);
            var item = viewModels[0];
            var visible = item.BeginRealization();
            AddReleasedRealizations(item, 64);
            using var start = new Barrier(2);
            var completing = Task.Run(() =>
            {
                Assert.IsTrue(start.SignalAndWait(TimeSpan.FromSeconds(5)));
                if (cleanup)
                {
                    item.SafeCleanup();
                }
                else
                {
                    item.InitializePropertiesOnce();
                }
            });
            var replacing = Task.Run(() =>
            {
                Assert.IsTrue(start.SignalAndWait(TimeSpan.FromSeconds(5)));
                return new ListItemInitializationCoordinator(viewModels);
            });

            try
            {
                await Task.WhenAll(completing, replacing).WaitAsync(TimeSpan.FromSeconds(10));
                previous.Stop();
                var replacement = await replacing;

                Assert.AreEqual(0, GetRetainedDemandNodes(item).Length);
                Assert.IsTrue(item.IsInitializationComplete);
                Assert.AreEqual(!cleanup, await item.WaitForInitializationAsync(CancellationToken.None));
                replacement.Run(CancellationToken.None);
                Assert.AreEqual(cleanup ? 0 : 1, models[0].InitializationCount);
            }
            finally
            {
                visible.Release();
                previous.Stop();
                if (replacing.IsCompletedSuccessfully)
                {
                    replacing.Result.Stop();
                }
            }
        }
    }

    [TestMethod]
    [Timeout(30000)]
    public async Task InitializationCompletionCanRaceTheFirstWaiter()
    {
        var (_, viewModels) = CreateItems(1024, new ConcurrentQueue<int>());
        using var barrier = new Barrier(2);
        var initializer = Task.Factory.StartNew(
            () =>
            {
                foreach (var item in viewModels)
                {
                    Assert.IsTrue(barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
                    item.InitializePropertiesOnce();
                    Assert.IsTrue(barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
                }
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var waiter = Task.Factory.StartNew(
            () =>
            {
                var stranded = 0;
                foreach (var item in viewModels)
                {
                    Assert.IsTrue(barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
                    var completion = item.WaitForInitializationAsync(CancellationToken.None);
                    Assert.IsTrue(barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
                    if (!completion.IsCompletedSuccessfully || !completion.Result)
                    {
                        stranded++;
                    }
                }

                return stranded;
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        await Task.WhenAll(initializer, waiter).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.AreEqual(0, await waiter);
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task ListRefreshDoesNotAbandonSelectedItemSlowInitialization()
    {
        using var initializationStarted = new ManualResetEventSlim();
        using var continueInitialization = new ManualResetEventSlim();
        var order = new ConcurrentQueue<int>();
        var items = new IListItem[48];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = new TrackingListItem(
                i,
                order,
                i == 20 ? initializationStarted : null,
                i == 20 ? continueInitialization : null);
        }

        var page = new RefreshablePage(items) { Id = "initialization.refresh", Name = "Initialization refresh" };
        var viewModel = new ListViewModel(page, TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance);
        var slowInitialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var itemsPublished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool>? blockedInitialization = null;
        ListItemViewModel? selected = null;

        void OnItemsUpdated(ListViewModel sender, ItemsUpdatedEventArgs args) => itemsPublished.TrySetResult();

        void OnSelectedPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ListItemViewModel.TextToSuggest) && selected?.TextToSuggest == "item-47")
            {
                slowInitialized.TrySetResult();
            }
        }

        try
        {
            viewModel.ItemsUpdated += OnItemsUpdated;
            viewModel.InitializeProperties();
            await itemsPublished.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsTrue(initializationStarted.Wait(TimeSpan.FromSeconds(2)));
            blockedInitialization = viewModel.FilteredItems.Single(item => ReferenceEquals(item.Model.Unsafe, items[20]))
                .WaitForInitializationAsync(CancellationToken.None);
            selected = viewModel.FilteredItems.Single(item => ReferenceEquals(item.Model.Unsafe, items[47]));
            selected.PropertyChangedBackground += OnSelectedPropertyChanged;

            viewModel.UpdateSelectedItemCommand.Execute(selected);
            page.Refresh();
            await slowInitialized.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual("item-47", selected.TextToSuggest);
        }
        finally
        {
            continueInitialization.Set();
            viewModel.ItemsUpdated -= OnItemsUpdated;
            viewModel.Dispose();
            if (blockedInitialization is not null)
            {
                await blockedInitialization.WaitAsync(TimeSpan.FromSeconds(2));
            }

            if (selected is not null)
            {
                selected.PropertyChangedBackground -= OnSelectedPropertyChanged;
            }

            viewModel.SafeCleanup();
        }
    }

    private static void AddReleasedRealizations(ListItemViewModel item, int count)
    {
        for (var i = 0; i < count; i++)
        {
            item.BeginRealization().Release();
        }
    }

    private static ListItemInitializationDemandNode[] GetRetainedDemandNodes(ListItemViewModel item)
    {
        // Inspect instance storage only after producers and replay have quiesced;
        // retention is the behavior under test, not a new production-facing API.
        var field = typeof(ListItemViewModel).GetField("_initializationDemands", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("The item's demand storage was not found.");
        var nodes = new List<ListItemInitializationDemandNode>();
        for (var node = (ListItemInitializationDemandNode?)field.GetValue(item); node is not null; node = node.Next)
        {
            nodes.Add(node);
        }

        return nodes.ToArray();
    }

    private static (TrackingListItem[] Models, ListItemViewModel[] ViewModels) CreateItems(
        int count,
        ConcurrentQueue<int> initializationOrder,
        ManualResetEventSlim? firstInitializationStarted = null,
        ManualResetEventSlim? continueFirstInitialization = null)
    {
        var models = new TrackingListItem[count];
        var viewModels = new ListItemViewModel[count];
        for (var i = 0; i < count; i++)
        {
            models[i] = new(
                i,
                initializationOrder,
                i == 0 ? firstInitializationStarted : null,
                i == 0 ? continueFirstInitialization : null);

            // View models intentionally retain only a weak page-context reference.
            // Keep the stateless test context alive during allocation-heavy races.
            viewModels[i] = new(models[i], new(TestContext), DefaultContextMenuFactory.Instance);
            Assert.IsTrue(viewModels[i].SafeFastInit());
        }

        return (models, viewModels);
    }
}
