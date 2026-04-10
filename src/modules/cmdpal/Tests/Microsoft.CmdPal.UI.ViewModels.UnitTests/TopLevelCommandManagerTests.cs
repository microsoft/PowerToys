// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class TopLevelCommandManagerTests
{
    private Mock<ISettingsService> _mockSettingsService = null!;
    private ILogger<TopLevelCommandManager> _logger = null!;
    private FakeExtensionService _fakeService = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockSettingsService
            .Setup(s => s.Settings)
            .Returns(CreateMinimalSettingsModel());
        _mockSettingsService
            .Setup(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()));

        _logger = NullLogger<TopLevelCommandManager>.Instance;
        _fakeService = new FakeExtensionService();
    }

    [TestCleanup]
    public void Cleanup()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    private static SettingsModel CreateMinimalSettingsModel()
    {
        var minimalJson = "{}";
        return System.Text.Json.JsonSerializer.Deserialize(
            minimalJson,
            JsonSerializationContext.Default.SettingsModel) ?? new SettingsModel();
    }

    private TopLevelCommandManager CreateManager(params IExtensionService[] services)
    {
        return new TopLevelCommandManager(
            services,
            _mockSettingsService.Object,
            TaskScheduler.Default,
            _logger);
    }

    [TestMethod]
    public async Task LoadExtensionsAsync_CallsSignalStartOnEachService()
    {
        var fake1 = new FakeExtensionService();
        var fake2 = new FakeExtensionService();
        using var manager = CreateManager(fake1, fake2);

        await manager.LoadExtensionsAsync();
        await Task.Delay(200);

        Assert.IsTrue(fake1.SignalStartCalled, "SignalStartExtensionsAsync should be called on first service");
        Assert.IsTrue(fake2.SignalStartCalled, "SignalStartExtensionsAsync should be called on second service");
    }

    [TestMethod]
    public async Task LoadExtensionsAsync_SetsIsLoadingToFalse()
    {
        using var manager = CreateManager(_fakeService);

        await manager.LoadExtensionsAsync();

        Assert.IsFalse(manager.IsLoading, "IsLoading should be false after extensions finish loading");
    }

    [TestMethod]
    public async Task OnCommandProviderAdded_AddsToCommandProviders()
    {
        using var manager = CreateManager(_fakeService);
        await manager.LoadExtensionsAsync();

        var wrapper = CreateStubWrapper("provider-add-test");

        _fakeService.FireOnCommandProviderAdded(new[] { wrapper });

        var providers = manager.CommandProviders.ToList();
        Assert.AreEqual(1, providers.Count, "CommandProviders should contain the added wrapper");
        Assert.AreSame(wrapper, providers[0]);
    }

    [TestMethod]
    public async Task OnCommandProviderRemoved_RemovesFromCommandProviders()
    {
        using var manager = CreateManager(_fakeService);
        await manager.LoadExtensionsAsync();

        var wrapper = CreateStubWrapper("provider-remove-test");
        _fakeService.FireOnCommandProviderAdded(new[] { wrapper });
        Assert.AreEqual(1, manager.CommandProviders.Count());

        _fakeService.FireOnCommandProviderRemoved(new[] { wrapper });

        Assert.AreEqual(0, manager.CommandProviders.Count(), "CommandProviders should be empty after removal");
    }

    [TestMethod]
    public async Task OnCommandProviderAdded_MultipleTimes_AccumulatesWrappers()
    {
        using var manager = CreateManager(_fakeService);
        await manager.LoadExtensionsAsync();

        var wrapper1 = CreateStubWrapper("multi-1");
        var wrapper2 = CreateStubWrapper("multi-2");

        _fakeService.FireOnCommandProviderAdded(new[] { wrapper1 });
        _fakeService.FireOnCommandProviderAdded(new[] { wrapper2 });

        Assert.AreEqual(2, manager.CommandProviders.Count());
    }

    [TestMethod]
    public void LookupCommand_EmptyCollection_ReturnsNull()
    {
        using var manager = CreateManager(_fakeService);

        Assert.IsNull(manager.LookupCommand("nonexistent"));
    }

    [TestMethod]
    public void LookupDockBand_EmptyCollection_ReturnsNull()
    {
        using var manager = CreateManager(_fakeService);

        Assert.IsNull(manager.LookupDockBand("nonexistent"));
    }

    [TestMethod]
    public async Task LookupProvider_WithLoadedWrapper_ReturnsMatch()
    {
        using var manager = CreateManager(_fakeService);
        await manager.LoadExtensionsAsync();

        var wrapper = CreateStubWrapper("lookup-provider-test");
        _fakeService.FireOnCommandProviderAdded(new[] { wrapper });

        var found = manager.LookupProvider(wrapper.ProviderId);

        Assert.IsNotNull(found);
        Assert.AreSame(wrapper, found);
    }

    [TestMethod]
    public async Task LookupProvider_NoMatch_ReturnsNull()
    {
        using var manager = CreateManager(_fakeService);
        await manager.LoadExtensionsAsync();

        Assert.IsNull(manager.LookupProvider("no-such-provider"));
    }

    [TestMethod]
    public async Task GetDockBandsSnapshot_EmptyByDefault()
    {
        using var manager = CreateManager(_fakeService);
        await manager.LoadExtensionsAsync();

        var snapshot = manager.GetDockBandsSnapshot();

        Assert.AreEqual(0, snapshot.Count);
    }

    [TestMethod]
    public async Task Dispose_UnsubscribesFromServices()
    {
        var fake = new FakeExtensionService();
        var manager = CreateManager(fake);
        await manager.LoadExtensionsAsync();

        manager.Dispose();

        var wrapper = CreateStubWrapper("after-dispose");
        fake.FireOnCommandProviderAdded(new[] { wrapper });

        Assert.AreEqual(
            0,
            manager.CommandProviders.Count(),
            "Events fired after Dispose should not be handled");
    }

    [TestMethod]
    public void Dispose_WithoutLoad_DoesNotThrow()
    {
        var manager = CreateManager(_fakeService);

        manager.Dispose();
    }

    [TestMethod]
    public async Task Receive_PinCommandItemMessage_LooksUpProvider()
    {
        using var manager = CreateManager(_fakeService);
        await manager.LoadExtensionsAsync();

        var wrapper = CreateStubWrapper("pin-test-provider");
        _fakeService.FireOnCommandProviderAdded(new[] { wrapper });

        var message = new PinCommandItemMessage(wrapper.ProviderId, "some-command-id");
        manager.Receive(message);

        _mockSettingsService.Verify(
            s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()),
            "PinCommand should delegate to ISettingsService.UpdateSettings");
    }

    [TestMethod]
    public async Task Receive_PinCommandItemMessage_UnknownProvider_DoesNotThrow()
    {
        using var manager = CreateManager(_fakeService);
        await manager.LoadExtensionsAsync();

        var message = new PinCommandItemMessage("unknown-provider", "some-command-id");
        manager.Receive(message);
    }

    [TestMethod]
    public async Task Receive_UnpinCommandItemMessage_UnknownProvider_DoesNotThrow()
    {
        using var manager = CreateManager(_fakeService);
        await manager.LoadExtensionsAsync();

        var message = new UnpinCommandItemMessage("unknown-provider", "cmd-id");
        manager.Receive(message);
    }

    [TestMethod]
    public async Task ReloadAllCommandsAsync_CallsStopThenStart()
    {
        var fake = new FakeExtensionService();
        using var manager = CreateManager(fake);
        await manager.LoadExtensionsAsync();

        fake.ResetTracking();

        await manager.ReloadAllCommandsAsync();
        await Task.Delay(300);

        Assert.IsTrue(fake.SignalStopCalled, "ReloadAllCommandsAsync should call SignalStopExtensionsAsync");
        Assert.IsTrue(fake.SignalStartCalled, "ReloadAllCommandsAsync should call SignalStartExtensionsAsync again after stop");
    }

    [TestMethod]
    public async Task ReloadAllCommandsAsync_ClearsTopLevelCommands()
    {
        using var manager = CreateManager(_fakeService);
        await manager.LoadExtensionsAsync();

        await manager.ReloadAllCommandsAsync();
        await Task.Delay(200);

        Assert.AreEqual(0, manager.TopLevelCommands.Count);
    }

    private static CommandProviderWrapper CreateStubWrapper(string id)
    {
        var stub = new StubCommandProvider(id);
        var logger = NullLogger<CommandProviderWrapper>.Instance;

        return new CommandProviderWrapper(
            stub,
            TaskScheduler.Default,
            null!,
            null!,
            logger);
    }

    /// <summary>
    /// Minimal in-proc command provider for constructing <see cref="CommandProviderWrapper"/>
    /// instances without WinRT/COM dependencies.
    /// </summary>
    private sealed partial class StubCommandProvider : CommandProvider
    {
        public StubCommandProvider(string id)
        {
            Id = id;
            DisplayName = $"Stub: {id}";
        }

        public override ICommandItem[] TopLevelCommands() => [];

        public override IFallbackCommandItem[]? FallbackCommands() => null;
    }

    /// <summary>
    /// Manually-controllable <see cref="IExtensionService"/> for test isolation.
    /// Stores event subscribers and exposes methods to fire events on demand.
    /// Tracks calls to <see cref="SignalStartExtensionsAsync"/> and
    /// <see cref="SignalStopExtensionsAsync"/>.
    /// </summary>
    private sealed class FakeExtensionService : IExtensionService
    {
        public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnCommandProviderAdded;

        public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnCommandProviderRemoved;

        public event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsAdded;

        public event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsRemoved;

        public bool SignalStartCalled { get; private set; }

        public bool SignalStopCalled { get; private set; }

        public Task SignalStartExtensionsAsync(WeakReference<IPageContext> weakPageContext)
        {
            SignalStartCalled = true;
            return Task.CompletedTask;
        }

        public Task SignalStopExtensionsAsync()
        {
            SignalStopCalled = true;
            return Task.CompletedTask;
        }

        public Task EnableProviderAsync(string providerId) => Task.CompletedTask;

        public Task DisableProviderAsync(string providerId) => Task.CompletedTask;

        public void ResetTracking()
        {
            SignalStartCalled = false;
            SignalStopCalled = false;
        }

        public void FireOnCommandProviderAdded(IEnumerable<CommandProviderWrapper> wrappers) =>
            OnCommandProviderAdded?.Invoke(this, wrappers);

        public void FireOnCommandProviderRemoved(IEnumerable<CommandProviderWrapper> wrappers) =>
            OnCommandProviderRemoved?.Invoke(this, wrappers);

        public void FireOnCommandsAdded(CommandProviderWrapper wrapper, IEnumerable<TopLevelViewModel> items) =>
            OnCommandsAdded?.Invoke(wrapper, items);

        public void FireOnCommandsRemoved(CommandProviderWrapper wrapper, IEnumerable<TopLevelViewModel> items) =>
            OnCommandsRemoved?.Invoke(wrapper, items);
    }
}
