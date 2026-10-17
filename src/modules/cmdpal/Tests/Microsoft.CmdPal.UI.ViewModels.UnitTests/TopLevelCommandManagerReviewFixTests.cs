// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class TopLevelCommandManagerTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoadExternalProvidersAsync_FailsDuringDisposal_LogsOnlyNonCancellation(bool canceled)
    {
        using var services = CreateServices();
        using var output = new StringWriter();
        using var listener = new TextWriterTraceListener(output);
        var release = new TaskCompletionSource<IEnumerable<CommandProviderWrapper>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).Returns(release.Task);
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        Exception failure = canceled
            ? new OperationCanceledException(new CancellationToken(canceled: true))
            : new InvalidOperationException("Startup service failed after cancellation.");
        Trace.Listeners.Add(listener);
        try
        {
            var load = manager.LoadExternalProvidersAsync();
            var disposal = manager.DisposeAsync().AsTask();
            Assert.IsFalse(disposal.IsCompleted);
            release.SetException(failure);
            var exception = await Assert.ThrowsAsync<Exception>(() => load.WaitAsync(LoadingTimeout));
            Assert.AreSame(failure, exception);
            Assert.AreEqual(canceled, load.IsCanceled);
            Assert.AreEqual(!canceled, load.IsFaulted);
            await disposal.WaitAsync(LoadingTimeout);
            Assert.IsFalse(manager.IsLoading);

            var log = output.ToString();
            Assert.AreEqual(!canceled, log.Contains("Failed to load external command providers", StringComparison.Ordinal));
            if (!canceled)
            {
                StringAssert.Contains(log, failure.Message);
            }
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            release.TrySetResult([]);
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReloadAllCommandsAsync_ThrowingLoadingObserver_PreservesAcceptedRunOutcome(bool fails)
    {
        using var services = CreateServices();
        using var output = new StringWriter();
        using var listener = new TextWriterTraceListener(output);
        var started = LoadingSignal();
        var release = LoadingSignal();
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CommandProviderWrapper>());
        service.Setup(s => s.SignalStopAsync()).Returns(() =>
        {
            started.TrySetResult();
            return release.Task;
        });
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        await manager.LoadExternalProvidersAsync();
        manager.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(manager.IsLoading))
            {
                throw new InvalidOperationException("Loading observer failed.");
            }
        };
        Trace.Listeners.Add(listener);
        try
        {
            var request = manager.ReloadAllCommandsAsync();
            await started.Task.WaitAsync(LoadingTimeout);
            Assert.IsFalse(request.IsCompleted, "The accepted run must determine the request's completion.");
            if (fails)
            {
                var failure = new InvalidOperationException("Reload failed.");
                release.SetException(failure);
                var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => request.WaitAsync(LoadingTimeout));
                Assert.AreSame(failure, exception);
            }
            else
            {
                release.SetResult();
                await request.WaitAsync(LoadingTimeout);
            }

            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
            Assert.IsFalse(manager.IsLoading);
            StringAssert.Contains(output.ToString(), "Failed to notify the command loading state");
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            release.TrySetResult();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    public async Task LoadTopLevelCommands_RepeatedLoads_ReturnsOnlyEachLoadsCreatedViewModels()
    {
        using var services = CreateServices();
        var firstItem = new Mock<ICommandItem>();
        firstItem.SetupGet(i => i.Command).Returns(new NoOpCommand { Id = "first-command" });
        var nextItem = new Mock<ICommandItem>();
        nextItem.SetupGet(i => i.Command).Returns(new NoOpCommand { Id = "next-command" });
        var provider = new LoadingTestCommandProvider { Commands = [firstItem.Object] };
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        var first = await wrapper.LoadTopLevelCommands(services);
        IReadOnlyList<TopLevelViewModel> next = [];
        try
        {
            provider.Commands = [nextItem.Object];
            next = await wrapper.LoadTopLevelCommands(services);
            Assert.HasCount(1, first);
            Assert.HasCount(1, next);
            Assert.AreEqual("first-command", first[0].Id);
            Assert.AreEqual("next-command", next[0].Id);
            Assert.AreSame(next[0], wrapper.TopLevelItems[0]);
            first[0].Cleanup();
            firstItem.VerifyRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
            nextItem.VerifyAdd(i => i.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
            nextItem.VerifyRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Never);
        }
        finally
        {
            foreach (var command in first)
            {
                command.Cleanup();
            }

            foreach (var command in next)
            {
                command.Cleanup();
            }
        }
    }
}
