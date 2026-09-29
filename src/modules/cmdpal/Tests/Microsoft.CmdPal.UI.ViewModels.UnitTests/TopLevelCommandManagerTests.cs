// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class TopLevelCommandManagerTests
{
    [TestMethod]
    public async Task WaitForCurrentLoadAsync_CompletesWhenCurrentLoadingPhaseFinishes()
    {
        using var services = CreateServices();
        using var manager = new TopLevelCommandManager(services, []);

        var waitTask = manager.WaitForCurrentLoadAsync();
        Assert.IsFalse(waitTask.IsCompleted);

        await manager.LoadExternalProvidersAsync();

        await waitTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsFalse(manager.IsLoading);
    }

    [TestMethod]
    public async Task WaitForCurrentLoadAsync_WhenNotLoading_CompletesImmediately()
    {
        using var services = CreateServices();
        using var manager = new TopLevelCommandManager(services, []);
        await manager.LoadExternalProvidersAsync();

        var waitTask = manager.WaitForCurrentLoadAsync();

        Assert.IsTrue(waitTask.IsCompletedSuccessfully);
    }

    [TestMethod]
    public async Task ResolveCommandAsync_UsesProviderLookupForNestedCommand()
    {
        using var services = CreateServices();
        var provider = new TestCommandProvider(TestCommandProvider.NestedCommandId);
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        using var manager = new TopLevelCommandManager(services, [CreateExtensionService(wrapper).Object]);
        await manager.LoadExternalProvidersAsync();

        using var resolution = await manager.ResolveCommandAsync(provider.Id, TestCommandProvider.NestedCommandId);

        Assert.IsNotNull(resolution);
        Assert.AreSame(wrapper, resolution.Provider);
        Assert.AreEqual(TestCommandProvider.NestedCommandId, resolution.Command.Id);
        Assert.AreEqual(1, provider.LookupCount);
    }

    [TestMethod]
    public async Task ResolveCommandAsync_RejectsMismatchedProviderResult()
    {
        using var services = CreateServices();
        var provider = new TestCommandProvider("different-command");
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        using var manager = new TopLevelCommandManager(services, [CreateExtensionService(wrapper).Object]);
        await manager.LoadExternalProvidersAsync();

        using var resolution = await manager.ResolveCommandAsync(provider.Id, TestCommandProvider.NestedCommandId);

        Assert.IsNull(resolution);
        Assert.AreEqual(1, provider.LookupCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ResolveCommandAsync_RejectsDisabledProviderWithCachedSettings(bool includeTopLevelCommand)
    {
        await using var services = CreateServices();
        var settingsService = services.GetRequiredService<ISettingsService>();
        var provider = new TestCommandProvider(TestCommandProvider.NestedCommandId)
        {
            IncludeTopLevelCommand = includeTopLevelCommand,
        };
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        using var manager = new TopLevelCommandManager(services, [CreateExtensionService(wrapper).Object]);
        await manager.LoadExternalProvidersAsync();

        settingsService.UpdateSettings(settings => settings with
        {
            ProviderSettings = settings.ProviderSettings.SetItem(provider.Id, new ProviderSettings(false)),
        });

        using var resolution = await manager.ResolveCommandAsync(provider.Id, TestCommandProvider.NestedCommandId);

        Assert.IsFalse(manager.IsProviderEnabled(provider.Id));
        Assert.IsNull(resolution);
        Assert.AreEqual(0, provider.LookupCount);
    }

    [TestMethod]
    public async Task ResolveCommandAsync_DoesNotReconstructCommandsAfterProviderIsDisabled()
    {
        await using var services = CreateServices();
        var settingsService = services.GetRequiredService<ISettingsService>();
        var provider = new TestCommandProvider(TestCommandProvider.NestedCommandId) { IncludeTopLevelCommand = true };
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        using var manager = new TopLevelCommandManager(services, [CreateExtensionService(wrapper).Object]);
        await manager.LoadExternalProvidersAsync();
        var providerSettings = new ProviderSettingsViewModel(wrapper, settingsService.Settings.ProviderSettings[provider.Id], settingsService);

        providerSettings.IsEnabled = false;
        using var resolution = await manager.ResolveCommandAsync(provider.Id, TestCommandProvider.NestedCommandId);

        Assert.IsNull(manager.LookupCommand(provider.Id, TestCommandProvider.NestedCommandId));
        Assert.IsNull(resolution);
        Assert.AreEqual(0, provider.LookupCount);

        providerSettings.IsEnabled = true;
        await manager.WaitForCurrentLoadAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var enabledResolution = await manager.ResolveCommandAsync(provider.Id, TestCommandProvider.NestedCommandId);
        Assert.IsNotNull(enabledResolution);
    }

    [TestMethod]
    public async Task ResolveCommandAsync_RejectsProviderDisabledDuringLookup()
    {
        await using var services = CreateServices();
        var settingsService = services.GetRequiredService<ISettingsService>();
        var provider = new TestCommandProvider(TestCommandProvider.NestedCommandId);
        provider.OnLookup = () => settingsService.UpdateSettings(settings => settings with
        {
            ProviderSettings = settings.ProviderSettings.SetItem(provider.Id, new ProviderSettings(false)),
        });
        var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
        using var manager = new TopLevelCommandManager(services, [CreateExtensionService(wrapper).Object]);
        await manager.LoadExternalProvidersAsync();

        using var resolution = await manager.ResolveCommandAsync(provider.Id, TestCommandProvider.NestedCommandId);

        Assert.IsNull(resolution);
        Assert.AreEqual(1, provider.LookupCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ResolveCommandAsync_RetriesProviderReplacedDuringLookupWithoutWaitingForCleanup(bool replacementHasTopLevelCommand)
    {
        await using var services = CreateServices();
        using var continueCleanup = new ManualResetEventSlim();
        var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var item = new Mock<ICommandItem>();
        item.SetupGet(commandItem => commandItem.Command).Returns(new NoOpCommand
        {
            Id = TestCommandProvider.NestedCommandId,
            Name = "Nested command",
        });
        item.SetupRemove(commandItem => commandItem.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback<TypedEventHandler<object, IPropChangedEventArgs>>(_ =>
            {
                cleanupStarted.TrySetResult();
                try
                {
                    Assert.IsTrue(continueCleanup.Wait(TimeSpan.FromSeconds(10)), "Cleanup was never released.");
                }
                finally
                {
                    cleanupFinished.TrySetResult();
                }
            });
        var provider = new TestCommandProvider(TestCommandProvider.NestedCommandId, item.Object);
        var original = new CommandProviderWrapper(provider, TaskScheduler.Default);
        var replacementProvider = new TestCommandProvider(TestCommandProvider.NestedCommandId)
        {
            IncludeTopLevelCommand = replacementHasTopLevelCommand,
        };
        var replacement = new CommandProviderWrapper(replacementProvider, TaskScheduler.Default);
        var extensionService = CreateExtensionService(original);
        extensionService
            .SetupSequence(service => service.LoadProvidersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([original])
            .ReturnsAsync([replacement]);
        using var manager = new TopLevelCommandManager(services, [extensionService.Object]);
        await manager.LoadExternalProvidersAsync();
        provider.OnLookup = () => manager.ReloadAllCommandsAsync().GetAwaiter().GetResult();

        var resolutionTask = manager.ResolveCommandAsync(provider.Id, TestCommandProvider.NestedCommandId);
        try
        {
            await cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var resolution = await resolutionTask.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.AreSame(replacement, manager.LookupProvider(provider.Id));
            Assert.IsNotNull(resolution);
            Assert.AreSame(replacement, resolution.Provider);
            Assert.AreSame(replacement, resolution.Command.ProviderContext);
            Assert.AreEqual(1, provider.LookupCount);
            Assert.AreEqual(replacementHasTopLevelCommand ? 0 : 1, replacementProvider.LookupCount);
            item.VerifyRemove(commandItem => commandItem.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Once);
        }
        finally
        {
            continueCleanup.Set();
            await cleanupFinished.Task.WaitAsync(TimeSpan.FromSeconds(2));
            using var resolution = await resolutionTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [TestMethod]
    public async Task ResolveCommandAsync_StopsAfterSecondProviderReplacement()
    {
        await using var services = CreateServices();
        using var cleanupFinished = new SemaphoreSlim(0);
        var item = new Mock<ICommandItem>();
        item.SetupGet(commandItem => commandItem.Command).Returns(new NoOpCommand
        {
            Id = TestCommandProvider.NestedCommandId,
            Name = "Nested command",
        });
        item.SetupRemove(commandItem => commandItem.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>())
            .Callback<TypedEventHandler<object, IPropChangedEventArgs>>(_ => cleanupFinished.Release());
        var provider = new TestCommandProvider(TestCommandProvider.NestedCommandId, item.Object);
        var original = new CommandProviderWrapper(provider, TaskScheduler.Default);
        var firstReplacement = new CommandProviderWrapper(provider, TaskScheduler.Default);
        var secondReplacement = new CommandProviderWrapper(provider, TaskScheduler.Default);
        var extensionService = CreateExtensionService(original);
        extensionService
            .SetupSequence(service => service.LoadProvidersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([original])
            .ReturnsAsync([firstReplacement])
            .ReturnsAsync([secondReplacement]);
        using var manager = new TopLevelCommandManager(services, [extensionService.Object]);
        await manager.LoadExternalProvidersAsync();
        provider.OnLookup = () => manager.ReloadAllCommandsAsync().GetAwaiter().GetResult();

        using var resolution = await manager.ResolveCommandAsync(provider.Id, TestCommandProvider.NestedCommandId);

        Assert.IsNull(resolution);
        Assert.AreSame(secondReplacement, manager.LookupProvider(provider.Id));
        Assert.AreEqual(2, provider.LookupCount);
        Assert.IsTrue(await cleanupFinished.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.IsTrue(await cleanupFinished.WaitAsync(TimeSpan.FromSeconds(2)));
        item.VerifyRemove(commandItem => commandItem.PropChanged -= It.IsAny<TypedEventHandler<object, IPropChangedEventArgs>>(), Times.Exactly(2));
    }

    [TestMethod]
    public async Task ProviderSettingsViewModel_ExposesExternalProviderId()
    {
        using var services = CreateServices();
        const string extensionProviderId = "external-provider";
        var provider = new TestCommandProvider(TestCommandProvider.NestedCommandId);
        var extension = new Mock<IExtensionWrapper>();
        extension.SetupGet(wrapper => wrapper.ExtensionUniqueId).Returns(extensionProviderId);
        extension.Setup(wrapper => wrapper.IsRunning()).Returns(true);
        extension.Setup(wrapper => wrapper.GetExtensionObject()).Returns(new TestExtension(provider));

        var wrapper = new CommandProviderWrapper(
            extension.Object,
            TaskScheduler.Default,
            Mock.Of<ICommandProviderCache>());
        var viewModel = new ProviderSettingsViewModel(
            wrapper,
            new ProviderSettings(),
            Mock.Of<ISettingsService>());

        Assert.AreEqual(string.Empty, viewModel.Id);
        Assert.AreEqual(extensionProviderId, viewModel.ProviderId);

        await wrapper.LoadTopLevelCommands(services);

        Assert.AreEqual(provider.Id, viewModel.Id);
        Assert.AreEqual(extensionProviderId, viewModel.ProviderId);
    }

    private static ServiceProvider CreateServices()
    {
        var settings = new SettingsModel();
        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(service => service.Settings).Returns(() => settings);
        settingsService.Setup(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((update, _) => settings = update(settings));

        return new ServiceCollection()
            .AddSingleton(TaskScheduler.Default)
            .AddSingleton(settingsService.Object)
            .BuildServiceProvider();
    }

    private static Mock<IExtensionService> CreateExtensionService(CommandProviderWrapper wrapper)
    {
        var extensionService = new Mock<IExtensionService>();
        extensionService
            .Setup(service => service.LoadProvidersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([wrapper]);
        return extensionService;
    }

    private sealed partial class TestCommandProvider : CommandProvider
    {
        public const string NestedCommandId = "nested-command";

        private readonly ICommandItem _resolvedItem;
        private int _lookupCount;

        public int LookupCount => _lookupCount;

        public bool IncludeTopLevelCommand { get; init; }

        public Action? OnLookup { get; set; }

        public TestCommandProvider(string resolvedCommandId, ICommandItem? resolvedItem = null)
        {
            Id = "test-provider";
            DisplayName = "Test provider";
            _resolvedItem = resolvedItem ?? new CommandItem(new NoOpCommand
            {
                Id = resolvedCommandId,
                Name = "Nested command",
            });
        }

        public override ICommandItem[] TopLevelCommands() => IncludeTopLevelCommand ? [_resolvedItem] : [];

        public override ICommandItem? GetCommandItem(string id)
        {
            Interlocked.Increment(ref _lookupCount);
            OnLookup?.Invoke();
            return id == NestedCommandId ? _resolvedItem : null;
        }
    }

    private sealed partial class TestExtension(ICommandProvider provider) : IExtension
    {
        public object GetProvider(ProviderType providerType) => provider;

        public void Dispose()
        {
        }
    }
}
