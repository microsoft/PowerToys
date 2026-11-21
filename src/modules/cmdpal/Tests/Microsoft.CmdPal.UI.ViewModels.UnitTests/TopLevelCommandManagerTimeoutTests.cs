// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Messages;
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
    [DataRow("command", false)]
    [DataRow("fallback", false)]
    [DataRow("band", false)]
    [DataRow("command", true)]
    [DataRow("fallback", true)]
    [DataRow("band", true)]
    public async Task ProviderEnable_Timeout_ReleasesLoadingAndDisposalAndCleansLateResults(string kind, bool disposeBeforeTimeout)
    {
        using var services = CreateServices();
        var provider = new LoadingTestCommandProvider();
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        var timeProvider = CreateEnableTimeProvider(out var timerCreated);
        using var manager = new TopLevelCommandManager(services, [CreateExtensionService(wrapper).Object], timeProvider);
        await manager.LoadExternalProvidersAsync();
        var started = LoadingSignal();
        var cleaned = LoadingSignal();
        using var release = new ManualResetEventSlim();
        var item = new Mock<ICommandItem>();
        item.SetupGet(i => i.Command).Returns(new NoOpCommand { Id = "timed-out-enable-command" });
        item.SetupRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback(() => cleaned.TrySetResult());
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
            await Task.Run(() => manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, true)));
            await started.Task.WaitAsync(LoadingTimeout);
            var expire = await timerCreated.WaitAsync(LoadingTimeout);
            var waiter = manager.WaitForCurrentLoadAsync();
            Task? disposal = null;
            if (disposeBeforeTimeout)
            {
                disposal = manager.DisposeAsync().AsTask();
                Assert.IsFalse(disposal.IsCompleted);
            }

            Assert.IsTrue(manager.IsLoading);
            Assert.IsFalse(waiter.IsCompleted);
            expire();
            await waiter.WaitAsync(LoadingTimeout);
            disposal ??= manager.DisposeAsync().AsTask();
            await disposal.WaitAsync(LoadingTimeout);
            Assert.IsFalse(manager.IsLoading);
            Assert.IsFalse(release.IsSet, "The hung command load must not hold loading or disposal past its timeout.");
            Assert.IsEmpty(manager.TopLevelCommands);
            Assert.IsEmpty(manager.DockBands);
            item.VerifyAdd(i => i.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Never);

            release.Set();
            await cleaned.Task.WaitAsync(LoadingTimeout);
            item.VerifyAdd(i => i.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
            item.VerifyRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
            Assert.IsEmpty(manager.TopLevelCommands);
            Assert.IsEmpty(manager.DockBands);
        }
        finally
        {
            release.Set();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    [DataRow("command")]
    [DataRow("fallback")]
    [DataRow("band")]
    public async Task ProviderEnable_CompletesBeforeTimeout_PublishesCommands(string kind)
    {
        using var services = CreateServices();
        var provider = new LoadingTestCommandProvider();
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        var timeProvider = CreateEnableTimeProvider(out var timerCreated);
        using var manager = new TopLevelCommandManager(services, [CreateExtensionService(wrapper).Object], timeProvider);
        await manager.LoadExternalProvidersAsync();
        var started = LoadingSignal();
        using var release = new ManualResetEventSlim();
        var item = new Mock<ICommandItem>();
        if (kind == "fallback")
        {
            item.As<IFallbackCommandItem>();
        }

        item.SetupGet(i => i.Command).Returns(new NoOpCommand { Id = "enabled-before-timeout" });
        SetLoadingProviderItem(provider, kind, item.Object);
        provider.OnLoad = () =>
        {
            started.TrySetResult();
            release.Wait();
        };
        try
        {
            await Task.Run(() => manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, true)));
            await started.Task.WaitAsync(LoadingTimeout);
            await timerCreated.WaitAsync(LoadingTimeout);
            var waiter = manager.WaitForCurrentLoadAsync();
            Assert.IsFalse(waiter.IsCompleted);
            release.Set();
            await waiter.WaitAsync(LoadingTimeout);
            Assert.IsFalse(manager.IsLoading);
            var published = kind == "band" ? manager.DockBands[0] : manager.TopLevelCommands[0];
            Assert.AreEqual("enabled-before-timeout", published.Id);
            Assert.HasCount(1, GetLoadingProviderItems(wrapper, kind));
            Assert.AreSame(published, GetLoadingProviderItems(wrapper, kind)[0]);
            item.VerifyRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Never);
        }
        finally
        {
            release.Set();
            await manager.WaitForCurrentLoadAsync().WaitAsync(LoadingTimeout);
            manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, false));
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    [TestMethod]
    public async Task ProviderEnable_FaultsAfterTimeout_ObservesAndLogsFailure()
    {
        using var services = CreateServices();
        var provider = new LoadingTestCommandProvider();
        var extension = new Mock<IExtensionWrapper>();
        extension.SetupGet(e => e.ExtensionUniqueId).Returns(provider.Id);
        extension.Setup(e => e.IsRunning()).Returns(true);
        extension.Setup(e => e.GetExtensionObject()).Returns(new TestExtension(provider));
        var wrapper = new CommandProviderWrapper(extension.Object, TaskScheduler.Default, Mock.Of<ICommandProviderCache>());
        var timeProvider = CreateEnableTimeProvider(out var timerCreated);
        using var manager = new TopLevelCommandManager(services, [CreateExtensionService(wrapper).Object], timeProvider);
        await manager.LoadExternalProvidersAsync();
        var started = LoadingSignal();
        using var release = new ManualResetEventSlim();
        var failure = new InvalidOperationException("The disconnected extension cannot report its package name.");
        extension.SetupGet(e => e.PackageFamilyName).Throws(failure);
        provider.OnLoad = () =>
        {
            started.TrySetResult();
            release.Wait();
            throw new InvalidOperationException("The command load failed after timing out.");
        };
        var logged = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new Mock<TraceListener>();
        listener.Setup(l => l.WriteLine(It.IsAny<string>())).Callback<string>(message =>
        {
            if (message.Contains("Failed to complete a timed-out enable", StringComparison.Ordinal))
            {
                logged.TrySetResult(message);
            }
        });
        Trace.Listeners.Add(listener.Object);
        try
        {
            await Task.Run(() => manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, true)));
            await started.Task.WaitAsync(LoadingTimeout);
            var expire = await timerCreated.WaitAsync(LoadingTimeout);
            expire();
            await manager.WaitForCurrentLoadAsync().WaitAsync(LoadingTimeout);
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
            release.Set();
            var log = await logged.Task.WaitAsync(LoadingTimeout);
            StringAssert.Contains(log, failure.Message);
            Assert.IsEmpty(manager.TopLevelCommands);
            Assert.IsEmpty(manager.DockBands);
        }
        finally
        {
            release.Set();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
            Trace.Listeners.Remove(listener.Object);
            listener.Object.Dispose();
        }
    }

    [TestMethod]
    public async Task ReloadAllCommandsAsync_ThrowingCancellationCallback_ReplacesGenerationAndDisposesPrevious()
    {
        using var services = CreateServices();
        var provider = new LoadingTestCommandProvider();
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        var failure = new InvalidOperationException("Generation cancellation callback failed.");
        var tokens = new List<CancellationToken>();
        var service = new Mock<IExtensionService>();
        service.Setup(s => s.LoadProvidersAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken token) =>
        {
            tokens.Add(token);
            if (tokens.Count == 1)
            {
                token.Register(() => throw failure);
                return Task.FromResult<IEnumerable<CommandProviderWrapper>>([wrapper]);
            }

            return Task.FromResult<IEnumerable<CommandProviderWrapper>>([]);
        });
        using var manager = new TopLevelCommandManager(services, [service.Object]);
        await manager.LoadExternalProvidersAsync();
        var exception = await Assert.ThrowsExactlyAsync<AggregateException>(() => manager.ReloadAllCommandsAsync().WaitAsync(LoadingTimeout));
        Assert.AreSame(failure, exception.InnerException);
        Assert.IsTrue(tokens[0].IsCancellationRequested);
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
        {
            _ = tokens[0].WaitHandle;
        });
        await manager.WaitForCurrentLoadAsync().WaitAsync(LoadingTimeout);

        provider.Commands = [new CommandItem(new NoOpCommand { Id = "enabled-after-cancellation-failure" })];
        try
        {
            manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, true));
            await manager.WaitForCurrentLoadAsync().WaitAsync(LoadingTimeout);
            Assert.HasCount(1, manager.TopLevelCommands);
            Assert.AreEqual("enabled-after-cancellation-failure", manager.TopLevelCommands[0].Id);
            manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, false));
            await manager.WaitForCurrentLoadAsync().WaitAsync(LoadingTimeout);
            Assert.IsEmpty(manager.TopLevelCommands);

            await manager.LoadExternalProvidersAsync().WaitAsync(LoadingTimeout);
            Assert.HasCount(2, tokens);
            Assert.AreNotEqual(tokens[0], tokens[1]);
            Assert.IsFalse(tokens[1].IsCancellationRequested);
            Assert.IsFalse(manager.IsLoading);
            service.Verify(s => s.SignalStopAsync(), Times.Never);
        }
        finally
        {
            manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, false));
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
        }
    }

    private static TimeProvider CreateEnableTimeProvider(out Task<Action> timerCreated)
    {
        var created = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
        timerCreated = created.Task;
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.CreateTimer(It.IsAny<TimerCallback>(), It.IsAny<object?>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
            .Returns((TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            {
                Assert.AreEqual(TimeSpan.FromSeconds(10), dueTime);
                Assert.AreEqual(Timeout.InfiniteTimeSpan, period);
                created.TrySetResult(() => callback(state));
                return Mock.Of<ITimer>();
            });
        return timeProvider.Object;
    }
}
