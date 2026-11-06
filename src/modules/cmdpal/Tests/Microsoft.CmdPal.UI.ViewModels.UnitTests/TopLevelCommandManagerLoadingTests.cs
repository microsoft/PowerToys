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
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[DoNotParallelize]
public partial class TopLevelCommandManagerTests
{
    private static readonly TimeSpan LoadingTimeout = TimeSpan.FromSeconds(5);
    private static readonly bool[] LoadingStartedNotifications = [true];
    private static readonly bool[] LoadingFinishedNotifications = [false];
    private static readonly bool[] LoadingCycleNotifications = [true, false];

    private static TaskCompletionSource LoadingSignal()
    {
        return new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    [TestMethod]
    public async Task ReloadAllCommandsAsync_Burst_KeepsLoadingUntilDrainedAndNotifiesOnce()
    {
        using var services = CreateServices();
        var starts = new[] { LoadingSignal(), LoadingSignal() };
        var releases = new[] { LoadingSignal(), LoadingSignal() };
        var stopped = 0;
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CommandProviderWrapper>());
        service.Setup(s => s.SignalStopAsync()).Returns(async () =>
        {
            var index = Interlocked.Increment(ref stopped) - 1;
            starts[index].SetResult();
            await releases[index].Task.ConfigureAwait(false);
        });
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        await manager.LoadExternalProvidersAsync();
        var notifications = ObserveLoadingChanges(manager);
        var recipient = new object();
        var finished = LoadingSignal();
        var finishedCount = 0;
        WeakReferenceMessenger.Default.Register<ReloadFinishedMessage>(recipient, (_, _) =>
        {
            Interlocked.Increment(ref finishedCount);
            finished.TrySetResult();
        });
        try
        {
            var first = manager.ReloadAllCommandsAsync();
            Assert.IsTrue(manager.IsLoading);
            var waiter = manager.WaitForCurrentLoadAsync();
            Assert.IsFalse(waiter.IsCompleted);
            await starts[0].Task.WaitAsync(LoadingTimeout);
            var second = manager.ReloadAllCommandsAsync();
            var third = manager.ReloadAllCommandsAsync();
            CollectionAssert.AreEqual(LoadingStartedNotifications, notifications.ToArray());
            releases[0].SetResult();
            await first.WaitAsync(LoadingTimeout);
            await starts[1].Task.WaitAsync(LoadingTimeout);
            Assert.IsTrue(manager.IsLoading);
            Assert.IsFalse(waiter.IsCompleted);
            Assert.AreEqual(0, Volatile.Read(ref finishedCount));
            releases[1].SetResult();
            await Task.WhenAll(second, third, waiter, finished.Task).WaitAsync(LoadingTimeout);
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
            Assert.IsFalse(manager.IsLoading);
            Assert.AreEqual(1, finishedCount);
            Assert.AreEqual(2, stopped);
            CollectionAssert.AreEqual(LoadingCycleNotifications, notifications.ToArray());
        }
        finally
        {
            foreach (var release in releases)
            {
                release.TrySetResult();
            }

            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }
    }

    [TestMethod]
    public async Task ReloadAllCommandsAsync_WithoutInitialExternalLoad_CompletesStartup()
    {
        using var services = CreateServices();
        await using var manager = new TopLevelCommandManager(services, []);
        var waiter = manager.WaitForCurrentLoadAsync();
        await manager.ReloadAllCommandsAsync().WaitAsync(LoadingTimeout);
        await waiter.WaitAsync(LoadingTimeout);
        Assert.IsFalse(manager.IsLoading);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReloadAllCommandsAsync_Failure_CompletesStartup(bool failsDuringLoad)
    {
        using var services = CreateServices();
        var failure = new InvalidOperationException("Reload failed.");
        var service = new Mock<IExtensionService>();
        if (failsDuringLoad)
        {
            service.Setup(s => s.SignalStopAsync()).Returns(Task.CompletedTask);
            service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        }
        else
        {
            service.Setup(s => s.SignalStopAsync()).ThrowsAsync(failure);
        }

        await using var manager = new TopLevelCommandManager(services, [service.Object]);
        var waiter = manager.WaitForCurrentLoadAsync();
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => manager.ReloadAllCommandsAsync().WaitAsync(LoadingTimeout));
        Assert.AreSame(failure, exception);
        await waiter.WaitAsync(LoadingTimeout);
        Assert.IsFalse(manager.IsLoading);
    }

    [TestMethod]
    public async Task ReloadAllCommandsAsync_CanceledDuringStartup_CompletesStartup()
    {
        using var services = CreateServices();
        var token = new CancellationToken(true);
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.SignalStopAsync()).Returns(Task.FromCanceled(token));
        await using var manager = new TopLevelCommandManager(services, [service.Object]);
        var waiter = manager.WaitForCurrentLoadAsync();
        var reload = manager.ReloadAllCommandsAsync();
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => reload.WaitAsync(LoadingTimeout));
        Assert.AreEqual(token, exception.CancellationToken);
        Assert.IsTrue(reload.IsCanceled);
        await waiter.WaitAsync(LoadingTimeout);
        Assert.IsFalse(manager.IsLoading);
    }

    [TestMethod]
    public async Task ReloadAllCommandsAsync_DuringExternalLoad_KeepsActiveLoadCount()
    {
        using var services = CreateServices();
        var release = LoadingSignal();
        var reloadStarted = LoadingSignal();
        var releaseReload = LoadingSignal();
        var loads = 0;
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.SignalStopAsync()).Returns(() =>
        {
            reloadStarted.SetResult();
            return releaseReload.Task;
        });
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).Returns(async () =>
        {
            if (Interlocked.Increment(ref loads) == 1)
            {
                await release.Task.ConfigureAwait(false);
            }

            return Array.Empty<CommandProviderWrapper>();
        });
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        var reload = manager.ReloadAllCommandsAsync();
        await reloadStarted.Task.WaitAsync(LoadingTimeout);
        var externalLoad = manager.LoadExternalProvidersAsync();
        var waiter = manager.WaitForCurrentLoadAsync();
        try
        {
            releaseReload.SetResult();
            await reload.WaitAsync(LoadingTimeout);
            Assert.IsTrue(manager.IsLoading);
            Assert.IsFalse(waiter.IsCompleted);
            release.SetResult();
            await Task.WhenAll(externalLoad, waiter).WaitAsync(LoadingTimeout);
            Assert.IsFalse(manager.IsLoading);
        }
        finally
        {
            releaseReload.TrySetResult();
            release.TrySetResult();
            await externalLoad.WaitAsync(LoadingTimeout);
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    public async Task LoadExternalProvidersAsync_OverlappingLoads_StayLoadingUntilBothFinish()
    {
        using var services = CreateServices();
        var releases = new[] { LoadingSignal(), LoadingSignal() };
        var loads = 0;
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).Returns(async () =>
        {
            var index = Interlocked.Increment(ref loads) - 1;
            await releases[index].Task.ConfigureAwait(false);
            return Array.Empty<CommandProviderWrapper>();
        });
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        var notifications = ObserveLoadingChanges(manager);
        var first = manager.LoadExternalProvidersAsync();
        var second = manager.LoadExternalProvidersAsync();
        try
        {
            var waiter = manager.WaitForCurrentLoadAsync();
            releases[0].SetResult();
            await first.WaitAsync(LoadingTimeout);
            Assert.IsTrue(manager.IsLoading);
            Assert.IsFalse(waiter.IsCompleted);
            releases[1].SetResult();
            await Task.WhenAll(second, waiter).WaitAsync(LoadingTimeout);
            Assert.IsFalse(manager.IsLoading);
            CollectionAssert.AreEqual(LoadingFinishedNotifications, notifications.ToArray());
        }
        finally
        {
            foreach (var release in releases)
            {
                release.TrySetResult();
            }

            await Task.WhenAll(first, second).WaitAsync(LoadingTimeout);
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    public async Task IsLoading_EnableDisableAndReload_IncludeAllActiveWork()
    {
        using var services = CreateServices();
        var provider = new LoadingTestCommandProvider();
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        var service = CreateExtensionService(wrapper);
        service.SetupSequence(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { wrapper })
            .ReturnsAsync(Array.Empty<CommandProviderWrapper>());
        var reloadStarted = LoadingSignal();
        var releaseReload = LoadingSignal();
        service.Setup(s => s.SignalStopAsync()).Returns(() =>
        {
            reloadStarted.SetResult();
            return releaseReload.Task;
        });
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        await manager.LoadExternalProvidersAsync();
        var notifications = ObserveLoadingChanges(manager);
        var enableStarted = LoadingSignal();
        using var releaseEnable = new ManualResetEventSlim();
        provider.OnLoad = () =>
        {
            enableStarted.TrySetResult();
            releaseEnable.Wait();
        };
        try
        {
            var reload = manager.ReloadAllCommandsAsync();
            await reloadStarted.Task.WaitAsync(LoadingTimeout);
            manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, true));
            await enableStarted.Task.WaitAsync(LoadingTimeout);
            manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, false));
            Assert.IsTrue(manager.IsLoading);
            var waiter = manager.WaitForCurrentLoadAsync();
            releaseReload.SetResult();
            await reload.WaitAsync(LoadingTimeout);
            Assert.IsTrue(manager.IsLoading);
            Assert.IsFalse(waiter.IsCompleted);
            releaseEnable.Set();
            await waiter.WaitAsync(LoadingTimeout);
            Assert.IsFalse(manager.IsLoading);
            CollectionAssert.AreEqual(LoadingCycleNotifications, notifications.ToArray());
        }
        finally
        {
            releaseEnable.Set();
            releaseReload.TrySetResult();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    public async Task IsLoading_Notification_AllowsConcurrentStateRead()
    {
        using var services = CreateServices();
        var release = LoadingSignal();
        var observation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CommandProviderWrapper>());
        service.Setup(s => s.SignalStopAsync()).Returns(release.Task);
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        await manager.LoadExternalProvidersAsync();
        manager.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(manager.IsLoading) && manager.IsLoading)
            {
                var read = Task.Run(() => manager.IsLoading);
                observation.TrySetResult(read.Wait(LoadingTimeout) && read.Result);
            }
        };
        try
        {
            var reload = manager.ReloadAllCommandsAsync();
            Assert.IsTrue(await observation.Task.WaitAsync(LoadingTimeout));
            release.SetResult();
            await reload.WaitAsync(LoadingTimeout);
        }
        finally
        {
            release.TrySetResult();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    public async Task Dispose_BlockedReload_DetachesImmediatelyAndCancelsPendingReload()
    {
        using var services = CreateServices();
        var started = LoadingSignal();
        var release = LoadingSignal();
        var stops = 0;
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CommandProviderWrapper>());
        service.Setup(s => s.SignalStopAsync()).Returns(() =>
        {
            Interlocked.Increment(ref stops);
            started.TrySetResult();
            return release.Task;
        });
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        await manager.LoadExternalProvidersAsync();
        var first = manager.ReloadAllCommandsAsync();
        try
        {
            await started.Task.WaitAsync(LoadingTimeout);
            var second = manager.ReloadAllCommandsAsync();
            manager.Dispose();
            var disposal = manager.DisposeAsync().AsTask();
            var secondDisposal = manager.DisposeAsync().AsTask();
            Assert.AreSame(disposal, secondDisposal);
            Assert.IsFalse(disposal.IsCompleted);
            Assert.IsTrue(manager.ReloadAllCommandsAsync().IsCanceled);
            Assert.IsFalse(WeakReferenceMessenger.Default.IsRegistered<ReloadCommandsMessage>(manager));
            Assert.IsFalse(WeakReferenceMessenger.Default.IsRegistered<ProviderEnabledStateChangedMessage>(manager));
            Assert.IsFalse(WeakReferenceMessenger.Default.IsRegistered<PinCommandItemMessage>(manager));
            Assert.IsFalse(WeakReferenceMessenger.Default.IsRegistered<UnpinCommandItemMessage>(manager));
            Assert.IsFalse(WeakReferenceMessenger.Default.IsRegistered<PinToDockMessage>(manager));
            service.VerifyRemove(s => s.OnProviderAdded -= It.IsAny<TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>>(), Times.Once);
            service.VerifyRemove(s => s.OnProviderRemoved -= It.IsAny<TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>>(), Times.Once);
            release.SetResult();
            await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(LoadingTimeout));
            await Assert.ThrowsAsync<OperationCanceledException>(() => second.WaitAsync(LoadingTimeout));
            Assert.IsTrue(second.IsCanceled);
            await disposal.WaitAsync(LoadingTimeout);
            Assert.AreEqual(1, stops);
            service.Verify(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            release.TrySetResult();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    public async Task DisposeAsync_ActiveProviderLoad_CancelsBeforeDrainingAndRejectsLateProviders()
    {
        using var services = CreateServices();
        var started = LoadingSignal();
        var canceled = LoadingSignal();
        var release = LoadingSignal();
        var loadCount = 0;
        var providerLoads = 0;
        var provider = new LoadingTestCommandProvider
        {
            OnLoad = () => Interlocked.Increment(ref providerLoads),
        };
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.SignalStopAsync()).Returns(Task.CompletedTask);
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).Returns(async (CancellationToken token) =>
        {
            if (Interlocked.Increment(ref loadCount) == 1)
            {
                return Array.Empty<CommandProviderWrapper>();
            }

            using var registration = token.Register(() => canceled.TrySetResult());
            started.SetResult();
            await release.Task.ConfigureAwait(false);
            Assert.IsTrue(token.WaitHandle.WaitOne(0));
            return new[] { wrapper };
        });
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        await manager.LoadExternalProvidersAsync();
        var first = manager.ReloadAllCommandsAsync();
        try
        {
            await started.Task.WaitAsync(LoadingTimeout);
            var second = manager.ReloadAllCommandsAsync();
            var disposal = manager.DisposeAsync().AsTask();
            await canceled.Task.WaitAsync(LoadingTimeout);
            Assert.IsFalse(disposal.IsCompleted);
            release.SetResult();
            await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(LoadingTimeout));
            await Assert.ThrowsAsync<OperationCanceledException>(() => second.WaitAsync(LoadingTimeout));
            Assert.IsTrue(second.IsCanceled);
            await disposal.WaitAsync(LoadingTimeout);
            Assert.AreEqual(2, loadCount);
            Assert.AreEqual(0, providerLoads);
            Assert.IsEmpty(manager.CommandProviders);
            service.Verify(s => s.SignalStopAsync(), Times.Once);
        }
        finally
        {
            release.TrySetResult();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    public async Task DisposeAsync_DuringGenerationCancellation_DoesNotCreateAnotherGeneration()
    {
        using var services = CreateServices();
        var cancellationStarted = LoadingSignal();
        using var releaseCancellation = new ManualResetEventSlim();
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken token) =>
        {
            token.Register(() =>
            {
                cancellationStarted.SetResult();
                releaseCancellation.Wait();
            });
            return Task.FromResult<IEnumerable<CommandProviderWrapper>>([]);
        });
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        await manager.LoadExternalProvidersAsync();
        var reload = manager.ReloadAllCommandsAsync();
        try
        {
            await cancellationStarted.Task.WaitAsync(LoadingTimeout);
            var disposal = manager.DisposeAsync().AsTask();
            Assert.IsFalse(disposal.IsCompleted);
            releaseCancellation.Set();
            await Assert.ThrowsAsync<OperationCanceledException>(() => reload.WaitAsync(LoadingTimeout));
            Assert.IsTrue(reload.IsCanceled);
            await disposal.WaitAsync(LoadingTimeout);
            service.Verify(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>()), Times.Once);
            service.Verify(s => s.SignalStopAsync(), Times.Never);
        }
        finally
        {
            releaseCancellation.Set();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    public async Task DisposeAsync_WaitsForCancellationCallbacksBeforeDisposingToken()
    {
        using var services = CreateServices();
        var started = LoadingSignal();
        using var release = new ManualResetEventSlim();
        var tokenWasAlive = false;
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken token) =>
        {
            token.Register(() =>
            {
                started.SetResult();
                release.Wait();
                tokenWasAlive = token.WaitHandle.WaitOne(0);
            });
            return Task.FromResult<IEnumerable<CommandProviderWrapper>>([]);
        });
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        await manager.LoadExternalProvidersAsync();
        try
        {
            var disposal = manager.DisposeAsync().AsTask();
            await started.Task.WaitAsync(LoadingTimeout);
            Assert.IsFalse(disposal.IsCompleted);
            release.Set();
            await disposal.WaitAsync(LoadingTimeout);
            Assert.IsTrue(tokenWasAlive);
        }
        finally
        {
            release.Set();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    public async Task Dispose_CancellationCallbackThrows_PreservesAwaitableFailure()
    {
        using var services = CreateServices();
        var failure = new InvalidOperationException("Cancellation failed.");
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken token) =>
        {
            token.Register(() => throw failure);
            return Task.FromResult<IEnumerable<CommandProviderWrapper>>([]);
        });
        var manager = new TopLevelCommandManager(services, [service.Object]);
        await manager.LoadExternalProvidersAsync();
        manager.Dispose();
        var exception = await Assert.ThrowsExactlyAsync<AggregateException>(() => manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout));
        Assert.AreSame(failure, exception.InnerException);
        Assert.IsFalse(WeakReferenceMessenger.Default.IsRegistered<ReloadCommandsMessage>(manager));
        service.VerifyRemove(s => s.OnProviderAdded -= It.IsAny<TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>>(), Times.Once);
    }

    [TestMethod]
    public async Task ReloadAllCommandsAsync_FailureWithPendingReload_KeepsLoadingUntilDrained()
    {
        using var services = CreateServices();
        var starts = new[] { LoadingSignal(), LoadingSignal() };
        var releases = new[] { LoadingSignal(), LoadingSignal() };
        var stops = 0;
        var failure = new InvalidOperationException("First reload failed.");
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CommandProviderWrapper>());
        service.Setup(s => s.SignalStopAsync()).Returns(async () =>
        {
            var index = Interlocked.Increment(ref stops) - 1;
            starts[index].SetResult();
            await releases[index].Task.ConfigureAwait(false);
            if (index == 0)
            {
                throw failure;
            }
        });
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        var notifications = ObserveLoadingChanges(manager);
        var waiter = manager.WaitForCurrentLoadAsync();
        try
        {
            var first = manager.ReloadAllCommandsAsync();
            await starts[0].Task.WaitAsync(LoadingTimeout);
            var second = manager.ReloadAllCommandsAsync();
            releases[0].SetResult();
            var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => first.WaitAsync(LoadingTimeout));
            Assert.AreSame(failure, exception);
            await starts[1].Task.WaitAsync(LoadingTimeout);
            Assert.IsTrue(manager.IsLoading);
            Assert.IsFalse(waiter.IsCompleted);
            Assert.IsEmpty(notifications);
            releases[1].SetResult();
            await Task.WhenAll(second, waiter).WaitAsync(LoadingTimeout);
            Assert.IsFalse(manager.IsLoading);
            CollectionAssert.AreEqual(LoadingFinishedNotifications, notifications.ToArray());
        }
        finally
        {
            foreach (var release in releases)
            {
                release.TrySetResult();
            }

            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReloadFinishedMessage_RequestFromLoadingObserver_WaitsForNextReload(bool concurrent)
    {
        using var services = CreateServices();
        var starts = new[] { LoadingSignal(), LoadingSignal() };
        var releases = new[] { LoadingSignal(), LoadingSignal() };
        var stops = 0;
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CommandProviderWrapper>());
        service.Setup(s => s.SignalStopAsync()).Returns(() =>
        {
            var index = Interlocked.Increment(ref stops) - 1;
            starts[index].SetResult();
            return releases[index].Task;
        });
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        await manager.LoadExternalProvidersAsync();
        Task? second = null;
        var requested = 0;
        manager.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(manager.IsLoading) && !manager.IsLoading && Interlocked.Exchange(ref requested, 1) == 0)
            {
                if (concurrent)
                {
                    Task.Run(() =>
                    {
                        second = manager.ReloadAllCommandsAsync();
                    }).WaitAsync(LoadingTimeout).GetAwaiter().GetResult();
                }
                else
                {
                    second = manager.ReloadAllCommandsAsync();
                }
            }
        };
        var recipient = new object();
        var finished = LoadingSignal();
        var finishedCount = 0;
        WeakReferenceMessenger.Default.Register<ReloadFinishedMessage>(recipient, (_, _) =>
        {
            Interlocked.Increment(ref finishedCount);
            finished.TrySetResult();
        });
        try
        {
            var first = manager.ReloadAllCommandsAsync();
            await starts[0].Task.WaitAsync(LoadingTimeout);
            releases[0].SetResult();
            await starts[1].Task.WaitAsync(LoadingTimeout);
            Assert.IsTrue(manager.IsLoading);
            Assert.AreEqual(0, Volatile.Read(ref finishedCount));
            Assert.IsNotNull(second);
            releases[1].SetResult();
            await Task.WhenAll(first, second, finished.Task).WaitAsync(LoadingTimeout);
            Assert.AreEqual(1, finishedCount);
        }
        finally
        {
            foreach (var release in releases)
            {
                release.TrySetResult();
            }

            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }
    }

    [TestMethod]
    public async Task DisposeAsync_DuringExternalLoad_DrainsLoadAndRejectsLateProviders()
    {
        using var services = CreateServices();
        var release = new TaskCompletionSource<IEnumerable<CommandProviderWrapper>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new LoadingTestCommandProvider();
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).Returns(release.Task);
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        var load = manager.LoadExternalProvidersAsync();
        try
        {
            var disposal = manager.DisposeAsync().AsTask();
            Assert.IsFalse(disposal.IsCompleted);
            Assert.IsTrue(manager.IsLoading);
            release.SetResult([wrapper]);
            await Assert.ThrowsAsync<OperationCanceledException>(() => load.WaitAsync(LoadingTimeout));
            await disposal.WaitAsync(LoadingTimeout);
            Assert.IsTrue(load.IsCanceled);
            Assert.IsFalse(manager.IsLoading);
            Assert.IsEmpty(manager.CommandProviders);
            Assert.IsEmpty(manager.TopLevelCommands);
            Assert.IsEmpty(manager.DockBands);
        }
        finally
        {
            release.TrySetResult([]);
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    [DataRow("command")]
    [DataRow("fallback")]
    [DataRow("band")]
    public async Task DisposeAsync_DuringProviderEnable_DrainsLoadAndCleansSubscriptions(string kind)
    {
        using var services = CreateServices();
        var provider = new LoadingTestCommandProvider();
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        using var manager = new TopLevelCommandManager(services, [CreateExtensionService(wrapper).Object]);
        await manager.LoadExternalProvidersAsync();
        var started = LoadingSignal();
        using var release = new ManualResetEventSlim();
        var item = new Mock<ICommandItem>();
        item.SetupGet(i => i.Command).Returns(new NoOpCommand { Id = "late-enable-command" });
        if (kind == "fallback")
        {
            provider.Fallbacks = [item.As<IFallbackCommandItem>().Object];
        }
        else if (kind == "band")
        {
            provider.Bands = [item.Object];
        }
        else
        {
            provider.Commands = [item.Object];
        }

        provider.OnLoad = () =>
        {
            started.TrySetResult();
            release.Wait();
        };
        try
        {
            manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, true));
            await started.Task.WaitAsync(LoadingTimeout);
            var disposal = manager.DisposeAsync().AsTask();
            Assert.IsFalse(disposal.IsCompleted);
            release.Set();
            await disposal.WaitAsync(LoadingTimeout);
            Assert.IsFalse(manager.IsLoading);
            Assert.IsEmpty(manager.TopLevelCommands);
            Assert.IsEmpty(manager.DockBands);
            item.VerifyAdd(i => i.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
            item.VerifyRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
            Assert.IsEmpty(wrapper.TopLevelItems);
            Assert.IsEmpty(wrapper.FallbackItems);
            Assert.IsEmpty(wrapper.DockBandItems);
        }
        finally
        {
            release.Set();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }

        Assert.IsEmpty(manager.TopLevelCommands);
    }

    [TestMethod]
    public async Task DisposeAsync_OverlappingActivityNotifications_WaitsForAllObservers()
    {
        using var services = CreateServices();
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CommandProviderWrapper>());
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        var started = LoadingSignal();
        using var release = new ManualResetEventSlim();
        var notifications = 0;
        manager.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(manager.IsLoading) && Interlocked.Increment(ref notifications) == 1)
            {
                started.TrySetResult();
                release.Wait();
            }
        };
        var first = Task.Run(manager.LoadExternalProvidersAsync);
        try
        {
            await started.Task.WaitAsync(LoadingTimeout);
            await manager.LoadExternalProvidersAsync().WaitAsync(LoadingTimeout);
            Assert.IsFalse(manager.IsLoading);
            var disposal = manager.DisposeAsync().AsTask();
            Assert.IsFalse(disposal.IsCompleted);
            release.Set();
            await Task.WhenAll(first, disposal).WaitAsync(LoadingTimeout);
            Assert.AreEqual(3, notifications);
        }
        finally
        {
            release.Set();
            await first.WaitAsync(LoadingTimeout);
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoadExternalProvidersAsync_ThrowingLoadingObserver_DoesNotFailLoadOrDisposal(bool throwWhenLoading)
    {
        using var services = CreateServices();
        using var manager = new TopLevelCommandManager(services, []);
        await manager.LoadExternalProvidersAsync();
        var failure = new InvalidOperationException("Loading observer failed.");
        manager.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(manager.IsLoading) && manager.IsLoading == throwWhenLoading)
            {
                throw failure;
            }
        };
        await manager.LoadExternalProvidersAsync().WaitAsync(LoadingTimeout);
        await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        Assert.IsFalse(manager.IsLoading);
    }

    private static ConcurrentQueue<bool> ObserveLoadingChanges(TopLevelCommandManager manager)
    {
        var changes = new ConcurrentQueue<bool>();
        manager.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(manager.IsLoading))
            {
                changes.Enqueue(manager.IsLoading);
            }
        };
        return changes;
    }

    private sealed partial class LoadingTestCommandProvider : CommandProvider
    {
        public Action? OnLoad { get; set; }

        public ICommandItem[] Commands { get; set; } = [];

        public IFallbackCommandItem[] Fallbacks { get; set; } = [];

        public ICommandItem[] Bands { get; set; } = [];

        public LoadingTestCommandProvider()
        {
            Id = "loading-test-provider";
            DisplayName = "Loading test provider";
        }

        public override ICommandItem[] TopLevelCommands()
        {
            OnLoad?.Invoke();
            return Commands;
        }

        public override IFallbackCommandItem[] FallbackCommands()
        {
            return Fallbacks;
        }

        public override ICommandItem[] GetDockBands()
        {
            return Bands;
        }
    }
}
