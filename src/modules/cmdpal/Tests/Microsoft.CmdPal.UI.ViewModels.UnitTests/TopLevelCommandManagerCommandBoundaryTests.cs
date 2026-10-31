// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class TopLevelCommandManagerTests
{
    [TestMethod]
    public async Task LoadExternalProvidersCommand_CanceledByReload_DoesNotRethrowFromExecute()
    {
        using var services = CreateServices();
        var loadStarted = LoadingSignal();
        var release = LoadingSignal();
        var loads = 0;
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.SignalStopAsync()).Returns(Task.CompletedTask);
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).Returns(async () =>
        {
            if (Interlocked.Increment(ref loads) == 1)
            {
                loadStarted.SetResult();
                await release.Task.ConfigureAwait(false);
            }

            return Array.Empty<CommandProviderWrapper>();
        });
        using var manager = new TopLevelCommandManager(services, [service.Object]);

        // AsyncRelayCommand.Execute rethrows a failed or canceled execution through an async void
        // method on the caller's SynchronizationContext. Without a context (PostLoadRootPageAsync
        // runs inside Task.Run) that rethrow is an unhandled thread-pool exception, which ends the
        // process. A recording context turns it into an observable failure.
        var context = new RecordingSynchronizationContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            // The same call PowerToysRootPageService.PostLoadRootPageAsync makes at startup
            manager.LoadExternalProvidersCommand.Execute(null);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        try
        {
            await loadStarted.Task.WaitAsync(LoadingTimeout);

            // A reload during the initial external load cancels that load's generation
            await manager.ReloadAllCommandsAsync().WaitAsync(LoadingTimeout);
            release.SetResult();

            var execution = manager.LoadExternalProvidersCommand.ExecutionTask!;
            await Assert.ThrowsAsync<OperationCanceledException>(() => execution.WaitAsync(LoadingTimeout));
            Assert.IsTrue(execution.IsCanceled);
            Assert.IsTrue(SpinWait.SpinUntil(() => context.IsDrained, LoadingTimeout), "The command's async void continuation did not finish.");

            Assert.IsEmpty(
                context.Exceptions,
                "LoadExternalProvidersCommand.Execute rethrew: " + string.Join(", ", context.Exceptions.Select(e => e.GetType().Name)));
        }
        finally
        {
            release.TrySetResult();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private int _operations;
        private int _pendingPosts;

        public ConcurrentQueue<Exception> Exceptions { get; } = new();

        public bool IsDrained => Volatile.Read(ref _operations) == 0 && Volatile.Read(ref _pendingPosts) == 0;

        public override void OperationStarted()
        {
            Interlocked.Increment(ref _operations);
        }

        public override void OperationCompleted()
        {
            Interlocked.Decrement(ref _operations);
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _pendingPosts);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var previous = Current;
                SetSynchronizationContext(this);
                try
                {
                    d(state);
                }
                catch (Exception ex)
                {
                    Exceptions.Enqueue(ex);
                }
                finally
                {
                    SetSynchronizationContext(previous);
                    Interlocked.Decrement(ref _pendingPosts);
                }
            });
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            try
            {
                d(state);
            }
            catch (Exception ex)
            {
                Exceptions.Enqueue(ex);
            }
        }
    }
}
