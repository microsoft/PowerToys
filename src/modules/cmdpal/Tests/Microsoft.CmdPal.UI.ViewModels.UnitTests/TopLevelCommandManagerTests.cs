// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

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

    private static ServiceProvider CreateServices()
    {
        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(service => service.Settings).Returns(new SettingsModel());

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

        public TestCommandProvider(string resolvedCommandId)
        {
            Id = "test-provider";
            DisplayName = "Test provider";
            _resolvedItem = new CommandItem(new NoOpCommand
            {
                Id = resolvedCommandId,
                Name = "Nested command",
            });
        }

        public override ICommandItem[] TopLevelCommands() => [];

        public override ICommandItem? GetCommandItem(string id)
        {
            Interlocked.Increment(ref _lookupCount);
            return id == NestedCommandId ? _resolvedItem : null;
        }
    }
}
