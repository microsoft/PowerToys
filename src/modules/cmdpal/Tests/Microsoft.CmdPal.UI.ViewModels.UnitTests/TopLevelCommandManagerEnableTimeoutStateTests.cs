// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class TopLevelCommandManagerTests
{
    [TestMethod]
    [DataRow("command")]
    [DataRow("fallback")]
    [DataRow("band")]
    public async Task ProviderEnable_LateResultAfterTimeout_LiveWrapperDoesNotExposeCleanedViewModels(string kind)
    {
        const string CommandId = "late-enable-command";
        using var services = CreateServices();
        var provider = new LoadingTestCommandProvider();
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        var timeProvider = CreateEnableTimeProvider(out var timerCreated);
        using var manager = new TopLevelCommandManager(services, [CreateExtensionService(wrapper).Object], timeProvider);
        await manager.LoadExternalProvidersAsync();

        var started = LoadingSignal();
        var cleaned = LoadingSignal();
        var published = LoadingSignal();
        using var release = new ManualResetEventSlim();
        var item = new Mock<ICommandItem>();
        if (kind == "fallback")
        {
            item.As<IFallbackCommandItem>();
        }

        item.SetupGet(i => i.Command).Returns(new NoOpCommand { Id = CommandId });
        item.SetupRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback(() => cleaned.TrySetResult());
        SetLoadingProviderItem(provider, kind, item.Object);
        provider.OnLoad = () =>
        {
            started.TrySetResult();
            release.Wait();
        };
        var collection = kind == "band" ? manager.DockBands : manager.TopLevelCommands;
        collection.CollectionChanged += (_, _) =>
        {
            if (collection.Any(c => c.Id == CommandId))
            {
                published.TrySetResult();
            }
        };

        try
        {
            await Task.Run(() => manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, true)));
            await started.Task.WaitAsync(LoadingTimeout);
            var expire = await timerCreated.WaitAsync(LoadingTimeout);
            expire();
            await manager.WaitForCurrentLoadAsync().WaitAsync(LoadingTimeout);
            Assert.IsFalse(manager.IsLoading);
            Assert.IsEmpty(collection);

            // Settings and dock pinning read the live wrapper's arrays directly
            release.Set();
            await Task.WhenAny(published.Task, cleaned.Task).WaitAsync(LoadingTimeout);
            Assert.IsTrue(published.Task.IsCompleted, "A still-current enable must publish its late result without another reload.");
            await manager.WaitForCurrentLoadAsync().WaitAsync(LoadingTimeout);

            var live = manager.CommandProviders.Single();
            Assert.AreSame(wrapper, live, "The enabled provider's wrapper is still the live one.");
            Assert.AreSame(collection.Single(), GetLoadingProviderItems(live, kind).Single());
            Assert.AreEqual(CommandId, collection.Single().Id);
            item.VerifyAdd(i => i.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
            item.VerifyRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Never);
        }
        finally
        {
            release.Set();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
            foreach (var command in GetLoadingProviderItems(wrapper, kind))
            {
                command.Cleanup();
            }
        }
    }

    [TestMethod]
    [DataRow("command")]
    [DataRow("fallback")]
    [DataRow("band")]
    public async Task ProviderEnable_LateResultAfterTimeout_DoesNotReplaceNewerWrapperItems(string kind)
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
        var lateItem = new Mock<ICommandItem>();
        var newerItem = new Mock<ICommandItem>();
        if (kind == "fallback")
        {
            lateItem.As<IFallbackCommandItem>();
            newerItem.As<IFallbackCommandItem>();
        }

        lateItem.SetupGet(i => i.Command).Returns(new NoOpCommand { Id = "shared-command-id" });
        newerItem.SetupGet(i => i.Command).Returns(new NoOpCommand { Id = "shared-command-id" });
        lateItem.SetupRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback(() => cleaned.TrySetResult());
        var loads = 0;
        provider.OnLoad = () =>
        {
            if (Interlocked.Increment(ref loads) == 1)
            {
                started.TrySetResult();
                release.Wait();
                SetLoadingProviderItem(provider, kind, lateItem.Object);
            }
            else
            {
                SetLoadingProviderItem(provider, kind, newerItem.Object);
            }
        };
        IReadOnlyList<TopLevelViewModel> newerCommands = [];
        try
        {
            await Task.Run(() => manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, true)));
            await started.Task.WaitAsync(LoadingTimeout);
            var expire = await timerCreated.WaitAsync(LoadingTimeout);
            expire();
            await manager.WaitForCurrentLoadAsync().WaitAsync(LoadingTimeout);

            newerCommands = await wrapper.LoadTopLevelCommands(services).WaitAsync(LoadingTimeout);
            var expected = newerCommands.Single();
            Assert.AreSame(expected, GetLoadingProviderItems(wrapper, kind).Single());
            release.Set();
            await cleaned.Task.WaitAsync(LoadingTimeout);

            Assert.AreSame(wrapper, manager.CommandProviders.Single());
            Assert.AreSame(expected, GetLoadingProviderItems(wrapper, kind).Single());
            lateItem.VerifyAdd(i => i.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
            lateItem.VerifyRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
            newerItem.VerifyAdd(i => i.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
            newerItem.VerifyRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Never);
        }
        finally
        {
            release.Set();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
            foreach (var command in newerCommands)
            {
                command.Cleanup();
            }
        }
    }

    [TestMethod]
    [DataRow("command", "disable")]
    [DataRow("fallback", "disable")]
    [DataRow("band", "disable")]
    [DataRow("command", "reload")]
    [DataRow("fallback", "reload")]
    [DataRow("band", "reload")]
    [DataRow("command", "newer-enable")]
    [DataRow("fallback", "newer-enable")]
    [DataRow("band", "newer-enable")]
    public async Task ProviderEnable_LateResultAfterTimeout_InvalidatedEnableCleansResults(string kind, string invalidation)
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
        var lateItem = new Mock<ICommandItem>();
        var newerItem = new Mock<ICommandItem>();
        if (kind == "fallback")
        {
            lateItem.As<IFallbackCommandItem>();
            newerItem.As<IFallbackCommandItem>();
        }

        lateItem.SetupGet(i => i.Command).Returns(new NoOpCommand { Id = "stale-enable-command" });
        newerItem.SetupGet(i => i.Command).Returns(new NoOpCommand { Id = "current-enable-command" });
        lateItem.SetupRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback(() => cleaned.TrySetResult());
        var loads = 0;
        provider.OnLoad = () =>
        {
            if (Interlocked.Increment(ref loads) == 1)
            {
                started.TrySetResult();
                release.Wait();
                SetLoadingProviderItem(provider, kind, lateItem.Object);
            }
            else
            {
                SetLoadingProviderItem(provider, kind, newerItem.Object);
            }
        };
        var collection = kind == "band" ? manager.DockBands : manager.TopLevelCommands;
        try
        {
            await Task.Run(() => manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, true)));
            await started.Task.WaitAsync(LoadingTimeout);
            var expire = await timerCreated.WaitAsync(LoadingTimeout);
            expire();
            await manager.WaitForCurrentLoadAsync().WaitAsync(LoadingTimeout);

            if (invalidation == "reload")
            {
                await manager.ReloadAllCommandsAsync().WaitAsync(LoadingTimeout);
            }
            else
            {
                await Task.Run(() => manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, invalidation == "newer-enable")));
            }

            await manager.WaitForCurrentLoadAsync().WaitAsync(LoadingTimeout);
            var expected = collection.ToArray();
            Assert.HasCount(invalidation == "disable" ? 0 : 1, expected);
            release.Set();
            await cleaned.Task.WaitAsync(LoadingTimeout);
            await manager.WaitForCurrentLoadAsync().WaitAsync(LoadingTimeout);

            CollectionAssert.AreEqual(expected, collection.ToArray());
            CollectionAssert.AreEqual(expected, GetLoadingProviderItems(wrapper, kind));
            lateItem.VerifyAdd(i => i.PropChanged += It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
            lateItem.VerifyRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
            newerItem.VerifyRemove(i => i.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Never);
        }
        finally
        {
            release.Set();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
            foreach (var command in GetLoadingProviderItems(wrapper, kind))
            {
                command.Cleanup();
            }
        }
    }

    [TestMethod]
    public async Task ProviderEnable_LateResultAfterTimeout_DisposalDrainsPublication()
    {
        using var services = CreateServices();
        var provider = new LoadingTestCommandProvider();
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        var timeProvider = CreateEnableTimeProvider(out var timerCreated);
        using var manager = new TopLevelCommandManager(services, [CreateExtensionService(wrapper).Object], timeProvider);
        await manager.LoadExternalProvidersAsync();
        var started = LoadingSignal();
        var publishing = LoadingSignal();
        using var releaseLoad = new ManualResetEventSlim();
        using var releasePublication = new ManualResetEventSlim();
        provider.Commands = [new CommandItem(new NoOpCommand { Id = "late-command" })];
        provider.OnLoad = () =>
        {
            started.TrySetResult();
            releaseLoad.Wait();
        };
        manager.TopLevelCommands.CollectionChanged += (_, _) =>
        {
            publishing.TrySetResult();
            releasePublication.Wait();
        };
        try
        {
            await Task.Run(() => manager.Receive(new ProviderEnabledStateChangedMessage(provider.Id, true)));
            await started.Task.WaitAsync(LoadingTimeout);
            var expire = await timerCreated.WaitAsync(LoadingTimeout);
            expire();
            await manager.WaitForCurrentLoadAsync().WaitAsync(LoadingTimeout);

            releaseLoad.Set();
            await publishing.Task.WaitAsync(LoadingTimeout);
            var disposal = manager.DisposeAsync().AsTask();
            Assert.IsFalse(disposal.IsCompleted, "An accepted late publication must finish before dependencies can be disposed.");
            releasePublication.Set();
            await disposal.WaitAsync(LoadingTimeout);
            Assert.IsFalse(manager.IsLoading);
            Assert.AreSame(manager.TopLevelCommands.Single(), wrapper.TopLevelItems.Single());
        }
        finally
        {
            releaseLoad.Set();
            releasePublication.Set();
            await manager.DisposeAsync().AsTask().WaitAsync(LoadingTimeout);
            foreach (var command in wrapper.TopLevelItems)
            {
                command.Cleanup();
            }
        }
    }

    private static void SetLoadingProviderItem(LoadingTestCommandProvider provider, string kind, ICommandItem item)
    {
        provider.Commands = kind == "command" ? [item] : [];
        provider.Fallbacks = kind == "fallback" ? [(IFallbackCommandItem)item] : [];
        provider.Bands = kind == "band" ? [item] : [];
    }

    private static TopLevelViewModel[] GetLoadingProviderItems(CommandProviderWrapper wrapper, string kind)
    {
        return kind switch
        {
            "fallback" => wrapper.FallbackItems,
            "band" => wrapper.DockBandItems,
            _ => wrapper.TopLevelItems,
        };
    }
}
