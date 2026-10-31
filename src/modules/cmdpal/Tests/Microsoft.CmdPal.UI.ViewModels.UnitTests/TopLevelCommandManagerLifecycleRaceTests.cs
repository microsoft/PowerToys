// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class TopLevelCommandManagerTests
{
    [TestMethod]
    public async Task ReloadFinishedMessage_RequestDuringIdleNotification_IsNotSentWhileReloadIsPending()
    {
        using var services = CreateServices();
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CommandProviderWrapper>());
        service.Setup(s => s.SignalStopAsync()).Returns(Task.CompletedTask);
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        await manager.LoadExternalProvidersAsync();

        // Pause before the manager observes idle so another request can arrive
        var idleObserved = LoadingSignal();
        using var resumeIdle = new ManualResetEventSlim();
        var armed = 1;
        PrependReloadBusyChangedHandler(manager, isBusy =>
        {
            if (!isBusy && Interlocked.Exchange(ref armed, 0) == 1)
            {
                idleObserved.SetResult();
                resumeIdle.Wait();
            }
        });

        var recipient = new object();
        var loadingWhenFinished = new ConcurrentQueue<bool>();
        var finished = LoadingSignal();
        WeakReferenceMessenger.Default.Register<ReloadFinishedMessage>(recipient, (_, _) =>
        {
            loadingWhenFinished.Enqueue(manager.IsLoading);
            finished.TrySetResult();
        });
        try
        {
            var first = manager.ReloadAllCommandsAsync();
            await idleObserved.Task.WaitAsync(LoadingTimeout);
            var second = manager.ReloadAllCommandsAsync();
            Assert.IsTrue(manager.IsLoading);
            resumeIdle.Set();

            await Task.WhenAll(first, second, finished.Task).WaitAsync(LoadingTimeout);
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);

            CollectionAssert.DoesNotContain(
                loadingWhenFinished.ToArray(),
                true,
                "ReloadFinishedMessage was sent while an accepted reload was still pending.");
            Assert.HasCount(1, loadingWhenFinished);
        }
        finally
        {
            resumeIdle.Set();
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    public async Task DisposeAsync_DuringProviderEnable_DoesNotAllowCommandsAddedAfterDisposalCompletes()
    {
        using var services = CreateServices();
        var provider = new BlockingTopLevelCommandProvider();
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        using var manager = new TopLevelCommandManager(services, [CreateExtensionService(wrapper).Object]);
        await manager.LoadExternalProvidersAsync();
        Assert.HasCount(1, manager.TopLevelCommands);

        manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, false));
        await manager.WaitForCurrentLoadAsync().WaitAsync(LoadingTimeout);
        Assert.IsEmpty(manager.TopLevelCommands);

        var loadStarted = LoadingSignal();
        using var releaseLoad = new ManualResetEventSlim();
        provider.OnLoad = () =>
        {
            loadStarted.TrySetResult();
            releaseLoad.Wait();
        };

        Task? disposal = null;
        var addedAfterDisposal = 0;
        manager.TopLevelCommands.CollectionChanged += (_, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Add && disposal is { IsCompleted: true })
            {
                Interlocked.Increment(ref addedAfterDisposal);
            }
        };

        try
        {
            manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, true));
            await loadStarted.Task.WaitAsync(LoadingTimeout);

            disposal = manager.DisposeAsync().AsTask();
            Assert.IsFalse(disposal.IsCompleted, "Disposal must drain the blocked enable activity.");
            releaseLoad.Set();
            await disposal.WaitAsync(LoadingTimeout);
            Assert.IsFalse(manager.IsLoading);
            Assert.IsEmpty(manager.TopLevelCommands);

            Assert.AreEqual(0, Volatile.Read(ref addedAfterDisposal), "Provider enable added commands after DisposeAsync completed.");
        }
        finally
        {
            releaseLoad.Set();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    // Test-only timing hook: puts a handler ahead of the manager's handler on the reload runner
    private static void PrependReloadBusyChangedHandler(TopLevelCommandManager manager, Action<bool> handler)
    {
        var runnerField = typeof(TopLevelCommandManager).GetField("_reloadCommandsRunner", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(runnerField, "TopLevelCommandManager no longer has _reloadCommandsRunner.");
        var runner = runnerField.GetValue(manager)!;
        var eventField = runner.GetType().GetField("BusyChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(eventField, "The reload runner no longer has a BusyChanged event field.");
        eventField.SetValue(runner, Delegate.Combine(handler, (Delegate?)eventField.GetValue(runner)));
    }

    private sealed partial class BlockingTopLevelCommandProvider : CommandProvider
    {
        private readonly ICommandItem _item = new CommandItem(new NoOpCommand
        {
            Id = "blocking-command",
            Name = "Blocking command",
        });

        public Action? OnLoad { get; set; }

        public BlockingTopLevelCommandProvider()
        {
            Id = "blocking-provider";
            DisplayName = "Blocking provider";
        }

        public override ICommandItem[] TopLevelCommands()
        {
            OnLoad?.Invoke();
            return [_item];
        }
    }
}
